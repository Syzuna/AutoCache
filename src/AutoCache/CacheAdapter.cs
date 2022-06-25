﻿using System;
using System.Threading.Tasks;
using AutoCache.Models;
using System.Collections.Concurrent;
using System.Threading;

namespace AutoCache
{
    public abstract class CacheAdapter : ICacheAdapter
    {
        private readonly TimeSpan _defaultOutdatedAt;
        private readonly TimeSpan _defaultExpireAt;
        private readonly TimeSpan _defaultTimeout;

        protected CacheAdapter(TimeSpan defaultOutdatedAt,
            TimeSpan defaultExpireAt, TimeSpan defaultTimeout)
        {
            _defaultOutdatedAt = defaultOutdatedAt;
            _defaultExpireAt = defaultExpireAt;
            _defaultTimeout = defaultTimeout;
        }
        public async Task<T> GetOrCreateAsync<T, TService>(
            string key,
            Func<Task<(T, bool)>> sourceFetch,
            TimeSpan? outdatedAt = null,
            TimeSpan? expireAt = null,
            TimeSpan? timeout = null)
        {
            var request = GetRequestParamsObject(sourceFetch, outdatedAt, expireAt);
            if (!string.IsNullOrEmpty(key))
            {
                Console.Log("start");
                // Fetch from cache
                var (cacheValue, cacheHit) = await GetAsync<CacheValue<T>>(key);

                // Cache hit and is not outdated
                if (cacheHit && !cacheValue.IsOutdated())
                {
                    Console.Log("cache hit");
                    return cacheValue.Value;
                }

                // Cache update 
                var sourceFetchTimeout = cacheHit
                    ? TimeSpan.Zero
                    : timeout ?? _defaultTimeout;
                var (updatedCacheValue, updatedCacheHit) = await ExecuteExclusiveTask(key, request, sourceFetchTimeout);
                Console.Log("update task executed.");
                if (updatedCacheHit)
                    (cacheValue, cacheHit) = (updatedCacheValue, updatedCacheHit);
                else Console.Log("task skiped.");
                Console.Log("update task " + (cacheHit ? "successed" : "failed") + " result " + cacheValue.Value);

                if (cacheHit)
                    return cacheValue.Value;
            }
            throw new Exception("Cache update timeout expired.");
        }

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks
            = new ConcurrentDictionary<string, SemaphoreSlim>();

        private async Task<(CacheValue<T>, bool)> ExecuteExclusiveTask<T>(
            string key,
            Request<T> request,
            TimeSpan waitMillisecondTimeout)
        {
            CacheValue<T> cacheValue = null;
            bool cacheHit = false;
            Console.Log("execute exclusive task. timeout:" + waitMillisecondTimeout);
            var semaphore = Locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            if (!await semaphore.WaitAsync(waitMillisecondTimeout))
            {
                Console.Log("execute exclusive task. doesn't wait and run the task");

            }
            else
            {
                try
                {
                    Console.Log("execute exclusive task. run task after waiting");
                    (cacheValue, cacheHit) = await GetFromCacheOrUpdateTheCache(key, request);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to run the background action.", ex);
                }
                finally
                {
                    semaphore.Release();
                }
            }
            return (cacheValue, cacheHit);
        }

        private Request<T> GetRequestParamsObject<T>(Func<Task<(T, bool)>> sourceFetch, TimeSpan? outdatedAt, TimeSpan? expireAt)
        {
            return new Request<T>(sourceFetch, outdatedAt, expireAt);
        }


        private async Task<(CacheValue<T>, bool)> GetFromCacheOrUpdateTheCache<T>(string key,
            Request<T> request)
        {
            // Fetch from cache
            var (cacheValue, cacheHit) = await GetAsync<CacheValue<T>>(key);

            // Update cache and get new value
            if (!cacheHit || cacheValue.IsOutdated())
                (cacheValue, cacheHit) = await UpdateTheCache(key, request);
            return (cacheValue, cacheHit);
        }

        private async Task<(CacheValue<T>, bool)> UpdateTheCache<T>(string key,
            Request<T> request)
        {
            // Get from source
            var (sourceValue, hasValue) = await request.SourceFetch();
            if (hasValue)
            {
                var cacheValue = ConvertSourceValueToCacheValue(sourceValue, request.OutdatedAt);
                await SetAsync(key, cacheValue, request.ExpireAt ?? _defaultExpireAt);
                return (cacheValue, true);
            }
            await RemoveAsync(key);
            return (default, false)!;
        }

        private CacheValue<T> ConvertSourceValueToCacheValue<T>(T sourceValue, TimeSpan? outdatedAt)
        {
            return new CacheValue<T>()
            {
                Value = sourceValue,
                OutdatedAt = DateTime.Now.Add(outdatedAt ?? _defaultOutdatedAt)
            };
        }

        public abstract Task RemoveAsync(string key);
        public abstract Task SetAsync<T>(string key, T value, TimeSpan expireAt);
        public abstract Task<(T, bool)> GetAsync<T>(string key);
    }
}