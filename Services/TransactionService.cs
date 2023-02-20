using LivestreamRecorderBackend.DB.Core;
using LivestreamRecorderBackend.DB.Enum;
using LivestreamRecorderBackend.DB.Exceptions;
using LivestreamRecorderBackend.DB.Interfaces;
using LivestreamRecorderBackend.DB.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LivestreamRecorderBackend.Services;

internal class TransactionService : IDisposable
{
    private static ILogger Logger => Helper.Log.Logger;
    private bool _disposedValue;
    private readonly IUnitOfWork _privateUnitOfWork;
    private readonly TransactionRepository _transactionRepository;
    private readonly IUnitOfWork _publicUnitOfWork;
    private readonly UserRepository _userRepositpry;
    private readonly ChannelRepository _channelRepository;
    private readonly VideoRepository _videoRepository;

    public TransactionService()
    {
        (_, _privateUnitOfWork) = Helper.Database.MakeDBContext<PrivateContext>();
        _transactionRepository = new TransactionRepository(_privateUnitOfWork);
        _userRepositpry = new UserRepository(_privateUnitOfWork);

        (_, _publicUnitOfWork) = Helper.Database.MakeDBContext<PublicContext>();
        _channelRepository = new ChannelRepository(_publicUnitOfWork);
        _videoRepository = new VideoRepository(_publicUnitOfWork);
    }

    /// <summary>
    /// Get Transaction By Id
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    /// <exception cref="EntityNotFoundException"></exception>
    internal Transaction GetTransactionById(string id) => _transactionRepository.GetById(id);

    internal IEnumerable<Transaction> GetTransactionsByUser(string userId)
        => _transactionRepository.Where(p => p.UserId == userId);

    internal IEnumerable<Transaction> GetTransactionsByChannel(string channelId)
        => _transactionRepository.Where(p => p.ChannelId == channelId);

    private Transaction InitNewTransaction(string userId, TokenType tokenType, TransactionType transactionType, decimal amount, string? channelId = null, string? videoId = null)
    {
        var transaction = new Transaction()
        {
            id = Guid.NewGuid().ToString(),
            TokenType = tokenType,
            UserId = userId,
            TransactionType = transactionType,
            Amount = amount,
            Timestamp = DateTime.UtcNow,
            TransactionState = TransactionState.Pending,
            ChannelId = channelId,
            VideoId = videoId
        };

        // Prevent GUID conflicts
        if (_transactionRepository.Exists(transaction.id)) transaction.id = Guid.NewGuid().ToString();

        var entry = _transactionRepository.Add(transaction);
        _privateUnitOfWork.Commit();
        Logger.Information("Init new transaction {TransactionId} for user {UserId}", transaction.id, userId);
        return entry.Entity;
    }

    internal string NewSupportChannelTransaction(string userId, string channelId, decimal amount)
    {
        Transaction supportTokenTransaction = InitNewTransaction(userId: userId,
                                                                 tokenType: TokenType.SupportToken,
                                                                 transactionType: TransactionType.Withdrawal,
                                                                 amount: amount,
                                                                 channelId: channelId);

        var user = _userRepositpry.GetById(userId);
        var channel = _channelRepository.GetById(channelId);
        channel.SupportToken ??= 0;

        // Insufficient balance
        if (user.Tokens.SupportToken < amount)
        {
            Logger.Warning("Insufficient balance when supporting channel {ChannelId} for user {UserId}", channelId, userId);
            supportTokenTransaction.TransactionState = TransactionState.Failed;
            supportTokenTransaction.Note = "Insufficient balance";
            Logger.Warning("Transaction failed: {TransactionId} {Note}", supportTokenTransaction.id, supportTokenTransaction.Note);

            _transactionRepository.Update(supportTokenTransaction);
            _privateUnitOfWork.Commit();
            return supportTokenTransaction.id;
        }

        Transaction downloadTokenTransaction = InitNewTransaction(userId: userId,
                                                                  tokenType: TokenType.DownloadToken,
                                                                  transactionType: TransactionType.Deposit,
                                                                  amount: amount,
                                                                  channelId: channelId);

        using (var scope = new System.Transactions.TransactionScope())
        {
            try
            {
                channel.SupportToken += amount;
                channel.Monitoring = true;
                _channelRepository.Update(channel);
                _publicUnitOfWork.Commit();
                Logger.Information("Successfully add {amount} support token to channel {ChannelId}", amount, channelId);

                user.Tokens.SupportToken -= amount;
                //_userRepositpry.Update(user);
                supportTokenTransaction.TransactionState = TransactionState.Success;
                supportTokenTransaction.Note = $"User {userId} support channel {channelId}";
                _transactionRepository.Update(supportTokenTransaction);
                //_privateUnitOfWork.Commit();
                Logger.Information("Success transaction {TransactionId} for user {UserId}", supportTokenTransaction.id, userId);

                user.Tokens.DownloadToken += amount;
                _userRepositpry.Update(user);
                downloadTokenTransaction.TransactionState = TransactionState.Success;
                downloadTokenTransaction.Note = $"User {userId} support channel {channelId}";
                _transactionRepository.Update(downloadTokenTransaction);
                _privateUnitOfWork.Commit();
                Logger.Information("Success transaction {TransactionId} for user {UserId}", supportTokenTransaction.id, userId);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Error when adding support token to channel {ChannelId}", channelId);

                supportTokenTransaction = _transactionRepository.GetById(supportTokenTransaction.id);   // For insurance
                downloadTokenTransaction = _transactionRepository.GetById(downloadTokenTransaction.id);   // For insurance
                supportTokenTransaction.TransactionState = TransactionState.Failed;
                downloadTokenTransaction.TransactionState = TransactionState.Failed;
                supportTokenTransaction.Note = $"Error when adding support token to channel {channelId}";
                downloadTokenTransaction.Note = $"Error when adding support token to channel {channelId}";
                Logger.Warning("Transaction failed: {TransactionId} {Note}", supportTokenTransaction.id, supportTokenTransaction.Note);

                _transactionRepository.Update(supportTokenTransaction);
                _transactionRepository.Update(downloadTokenTransaction);
                _privateUnitOfWork.Commit();
                return supportTokenTransaction.id;
            }

            scope.Complete();
        }

        return supportTokenTransaction.id;
    }

    internal string NewDownloadVideoTransaction(string userId, string videoId)
    {
        var video = _videoRepository.GetById(videoId);

        decimal amount = CalculateConsumeToken(video.Size);
        Transaction downloadTokenTransaction = InitNewTransaction(userId: userId,
                                                                 tokenType: TokenType.DownloadToken,
                                                                 transactionType: TransactionType.Withdrawal,
                                                                 amount: amount,
                                                                 channelId: video.ChannelId,
                                                                 videoId: videoId);

        var user = _userRepositpry.GetById(userId);
        if (user.Tokens.DownloadToken < amount)
        {
            Logger.Warning("Insufficient balance when downloading video {VideoId} for user {UserId}", videoId, userId);
            downloadTokenTransaction.TransactionState = TransactionState.Failed;
            downloadTokenTransaction.Note = "Insufficient balance";
            Logger.Warning("Transaction failed: {TransactionId} {Note}", downloadTokenTransaction.id, downloadTokenTransaction.Note);

            _transactionRepository.Update(downloadTokenTransaction);
            _privateUnitOfWork.Commit();
            return downloadTokenTransaction.id;
        }

        // Check if video archived date is after first channel support date
        Transaction? firstSupportTransaction = GetFirstSupportTransaction(video.ChannelId, user.id);
        if (null == firstSupportTransaction
           || firstSupportTransaction.Timestamp > video.ArchivedTime)
        {
            Logger.Warning("Permission was denied because the video {videoId} had been recorded prior to the timestamp when the user {userId} submitted the recording request.", videoId, userId);
            downloadTokenTransaction.TransactionState = TransactionState.Failed;
            downloadTokenTransaction.Note = "Permission was denied because the video had been recorded prior to the timestamp when the user submitted the recording request.";
            Logger.Warning("Transaction failed: {TransactionId} {Note}", downloadTokenTransaction.id, downloadTokenTransaction.Note);

            _transactionRepository.Update(downloadTokenTransaction);
            _privateUnitOfWork.Commit();
            return downloadTokenTransaction.id;
        }

        using (var scope = new System.Transactions.TransactionScope())
        {
            try
            {
                // Spend tokens
                user.Tokens.DownloadToken -= amount;
                _userRepositpry.Update(user);
                //_privateUnitOfWork.Commit();
                Logger.Information("User {user} successfully spend {amount} download tokens for the video {videoId}", user.id, amount, videoId);

                downloadTokenTransaction.TransactionState = TransactionState.Success;
                downloadTokenTransaction.Note = $"User {userId} downloaded video {videoId}@{video.ChannelId}";
                _transactionRepository.Update(downloadTokenTransaction);
                _privateUnitOfWork.Commit();
                Logger.Information("Success transaction {TransactionId} for user {UserId}", downloadTokenTransaction.id, userId);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Error when user {user} spend {amount} download tokens for the video {videoId}", user.id, amount, video.id);

                downloadTokenTransaction = _transactionRepository.GetById(downloadTokenTransaction.id);   // For insurance
                downloadTokenTransaction.TransactionState = TransactionState.Failed;
                downloadTokenTransaction.Note = $"Error when user {user} spend {amount} download tokens for the video {videoId}";
                Logger.Warning("Transaction failed: {TransactionId} {Note}", downloadTokenTransaction.id, downloadTokenTransaction.Note);

                _transactionRepository.Update(downloadTokenTransaction);
                _privateUnitOfWork.Commit();
                return downloadTokenTransaction.id;
            }

            scope.Complete();
        }

        return downloadTokenTransaction.id;
    }

    internal Transaction? GetFirstSupportTransaction(string channelId, string userId)
        => _transactionRepository.Where(p => p.UserId == userId
                                             && p.TransactionState == TransactionState.Success
                                             && p.TokenType == TokenType.SupportToken
                                             && p.TransactionType == TransactionType.Withdrawal
                                             && p.ChannelId == channelId)
                                 .OrderBy(p => p.Timestamp)
                                 .FirstOrDefault();

    private static decimal CalculateConsumeToken(long? size)
    {
        if (null == size) return 0m;

        decimal gb = (decimal)(size! / 1024.0m / 1024.0m / 1024.0m);
        return gb switch
        {
            < 0.5m => 0m,
            <= 1m => 1m,
            _ => Math.Floor(gb)
        };
    }

    internal string ClaimSupportTokens(string userId, decimal amount)
    {
        Transaction transaction = InitNewTransaction(userId: userId,
                                                     tokenType: TokenType.SupportToken,
                                                     transactionType: TransactionType.Deposit,
                                                     amount: amount);

        var user = _userRepositpry.GetById(userId);

        using (var scope = new System.Transactions.TransactionScope())
        {
            try
            {
                user.Tokens.SupportToken += amount;
                _userRepositpry.Update(user);
                transaction.TransactionState = TransactionState.Success;
                transaction.Note = $"User claims {amount} support tokens.";
                _transactionRepository.Update(transaction);
                _privateUnitOfWork.Commit();
                Logger.Information("Success transaction {TransactionId} for user {UserId}", transaction.id, userId);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Error when claiming support tokens for user {userId}", userId);

                transaction = _transactionRepository.GetById(transaction.id);   // For insurance
                transaction.TransactionState = TransactionState.Failed;
                transaction.Note = $"Error when claiming support tokens for user {userId}";
                Logger.Warning("Transaction failed: {TransactionId} {Note}", transaction.id, transaction.Note);

                _transactionRepository.Update(transaction);
                _privateUnitOfWork.Commit();
                return transaction.id;
            }

            scope.Complete();
        }

        return transaction.id;
    }

    internal bool IsVideoDownloaded(string vidoeId, string userId)
        => _transactionRepository.Where(p => p.UserId == userId
                                             && p.VideoId == vidoeId
                                             && p.TransactionState == TransactionState.Success
                                             && p.TokenType == TokenType.DownloadToken
                                             && p.TransactionType == TransactionType.Withdrawal)
                                 .ToList()
                                 .Any();

    #region Dispose
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _privateUnitOfWork.Context.Dispose();
                _publicUnitOfWork.Context.Dispose();
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        // 請勿變更此程式碼。請將清除程式碼放入 'Dispose(bool disposing)' 方法
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
    #endregion
}
