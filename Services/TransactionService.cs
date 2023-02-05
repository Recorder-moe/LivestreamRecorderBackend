using LivestreamRecorderBackend.DB.Core;
using LivestreamRecorderBackend.DB.Enum;
using LivestreamRecorderBackend.DB.Exceptions;
using LivestreamRecorderBackend.DB.Interfaces;
using LivestreamRecorderBackend.DB.Models;
using Serilog;
using System;
using System.Collections.Generic;

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

    public TransactionService()
    {
        (_, _privateUnitOfWork) = Helper.Database.MakeDBContext<PrivateContext>();
        _transactionRepository = new TransactionRepository(_privateUnitOfWork);
        _userRepositpry = new UserRepository(_privateUnitOfWork);

        (_, _publicUnitOfWork) = Helper.Database.MakeDBContext<PublicContext>();
        _channelRepository = new ChannelRepository(_publicUnitOfWork);
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

    private Transaction InitNewTransaction(string userId, TokenType tokenType, TransactionType transactionType, decimal amount, string? channelId = null)
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
        };

        // Prevent GUID conflicts
        if (_transactionRepository.Exists(transaction.id)) transaction.id = Guid.NewGuid().ToString();

        var entry = _transactionRepository.Add(transaction);
        _privateUnitOfWork.Commit();
        Logger.Information("Init new transaction {TransactionId} for user {UserId}", transaction.id, userId);
        return entry.Entity;
    }

    internal string SupportChannel(string userId, string channelId, decimal amount)
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
                _transactionRepository.Update(supportTokenTransaction);
                //_privateUnitOfWork.Commit();
                Logger.Information("Success transaction {TransactionId} for user {UserId}", supportTokenTransaction.id, userId);

                user.Tokens.DownloadToken += amount;
                _userRepositpry.Update(user);
                downloadTokenTransaction.TransactionState = TransactionState.Success;
                _transactionRepository.Update(downloadTokenTransaction);
                _privateUnitOfWork.Commit();
                Logger.Information("Success transaction {TransactionId} for user {UserId}", supportTokenTransaction.id, userId);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Error when adding support token to channel {ChannelId}", channelId);

                supportTokenTransaction = _transactionRepository.GetById(supportTokenTransaction.id);   // For insurance
                downloadTokenTransaction= _transactionRepository.GetById(downloadTokenTransaction.id);   // For insurance
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
