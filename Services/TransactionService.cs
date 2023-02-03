﻿using LivestreamRecorderBackend.DB.Core;
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

    internal string SupportChannel(string userId, string channelId, decimal amount)
    {
        Transaction transaction = InitNewTransaction(userId, channelId, amount);

        var user = _userRepositpry.GetById(userId);
        var channel = _channelRepository.GetById(channelId);
        channel.SupportToken ??= 0;

        // Insufficient balance
        if (user.Tokens.SupportToken < amount)
        {
            Logger.Warning("Insufficient balance when supporting channel {ChannelId} for user {UserId}", channelId, userId);
            transaction.TransactionState = TransactionState.Failed;
            transaction.Note = "Insufficient balance";
            Logger.Error("Transaction failed: {TransactionId} {Note}", transaction.id, transaction.Note);

            _transactionRepository.Update(transaction);
            _privateUnitOfWork.Commit();
            return transaction.id;
        }

        using (var scope = new System.Transactions.TransactionScope())
        {
            try
            {
                channel.SupportToken += amount;
                _channelRepository.Update(channel);
                _publicUnitOfWork.Commit();
                Logger.Information("Successfully add {amount} support token to channel {ChannelId}", amount, channelId);

                user.Tokens.SupportToken -= amount;
                _userRepositpry.Update(user);
                transaction.TransactionState = TransactionState.Success;
                _transactionRepository.Update(transaction);
                _privateUnitOfWork.Commit();
                Logger.Information("Success transaction {TransactionId} for user {UserId}", transaction.id, userId);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Error when adding support token to channel {ChannelId}", channelId);

                transaction = _transactionRepository.GetById(transaction.id);   // For insurance
                transaction.TransactionState = TransactionState.Failed;
                transaction.Note = $"Error when adding support token to channel {channelId}";
                Logger.Error("Transaction failed: {TransactionId} {Note}", transaction.id, transaction.Note);

                _transactionRepository.Update(transaction);
                _privateUnitOfWork.Commit();
                return transaction.id;
            }

            scope.Complete();
        }

        return transaction.id;
    }

    private Transaction InitNewTransaction(string userId, string channelId, decimal amount)
    {
        var transaction = new Transaction()
        {
            id = Guid.NewGuid().ToString(),
            TokenType = TokenType.SupportToken,
            UserId = userId,
            TransactionType = TransactionType.Withdrawal,
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