using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.Api {
    /// <summary>
    /// Use only for APIs! This class does not use Last-Modified/If-Modified-Since headers.
    /// </summary>
    public class ApiCacheThing {
        private readonly TimeSpan _cacheAliveTime;
        private readonly string _directory;

        private readonly Dictionary<string, Tuple<byte[], DateTime>> _cache =
                new Dictionary<string, Tuple<byte[], DateTime>>(10);

        public ApiCacheThing(string directoryName, TimeSpan cacheAliveTime) {
            _cacheAliveTime = cacheAliveTime;
            _directory = FilesStorage.Instance.GetTemporaryDirectory(directoryName);
        }

        [NotNull]
        private static string GetTemporaryName(string argument) {
            using (var sha1 = SHA1.Create()) {
                var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(argument));
                return BitConverter.ToString(hash).Replace(@"-", "").ToLower();
            }
        }

        public void Clear() {
            try {
                foreach (var file in Directory.GetFiles(_directory).Where(
                        x => Path.GetFileName(x).All(y => char.IsDigit(y) || y >= 'a' && y <= 'f' || y >= 'A' && y <= 'F'))) {
                    File.Delete(file);
                }

                lock (_cache) {
                    _cache.Clear();
                }
            } catch (Exception e) {
                Logging.Warning(e);
            }
        }

        [ItemCanBeNull]
        private async Task<byte[]> GetBytesAsyncInner([NotNull] string url, string cacheKey = null, TimeSpan? aliveTime = null,
                CancellationToken cancellation = default(CancellationToken)) {
            try {
                if (cacheKey == null) {
                    cacheKey = GetTemporaryName(url);
                }

                var actualAliveTime = aliveTime ?? _cacheAliveTime;

                Tuple<byte[], DateTime> cache;
                lock (_cache) {
                    cache = _cache.GetValueOrDefault(cacheKey);
                }
                if (cache != null && cache.Item2 > DateTime.Now - actualAliveTime) {
                    return cache.Item1;
                }

                var cacheFile = new FileInfo(FilesStorage.Instance.GetTemporaryFilename(_directory, cacheKey));
                if (cacheFile.Exists && cacheFile.LastWriteTime > DateTime.Now - actualAliveTime) {
                    try {
                        var data = await FileUtils.ReadAllBytesAsync(cacheFile.FullName).ConfigureAwait(false);
                        lock (_cache) {
                            _cache[cacheKey] = Tuple.Create(data, cacheFile.LastWriteTime);
                        }
                        return data;
                    } catch (Exception e) {
                        Logging.Warning(e);
                        try {
                            cacheFile.Delete();
                        } catch {
                            // ignored
                        }
                    }
                }

                try {
                    var request = new HttpRequestMessage(HttpMethod.Get, url);

                    using (var timeout = new CancellationTokenSource(KunosApiProvider.OptionWebRequestTimeout))
                    using (var combined = CancellationTokenSource.CreateLinkedTokenSource(cancellation, timeout.Token))
                    using (var response = await HttpClientHolder.Get().SendAsync(request, combined.Token).ConfigureAwait(false)) {
                        var data = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

                        lock (_cache) {
                            _cache[cacheKey] = Tuple.Create(data, DateTime.Now);
                        }

                        try {
                            File.WriteAllBytes(cacheFile.FullName, data);
                        } catch (Exception e) {
                            Logging.Warning(e);
                        }

                        return data;
                    }
                } catch (Exception e) {
                    if (!cancellation.IsCancellationRequested) {
                        Logging.Warning(e);
                    }

                    if (cache != null) {
                        return cache.Item1;
                    }

                    if (cacheFile.Exists) {
                        var data = await FileUtils.ReadAllBytesAsync(cacheFile.FullName).ConfigureAwait(false);
                        lock (_cache) {
                            _cache[cacheKey] = Tuple.Create(data, cacheFile.LastWriteTime);
                        }
                        return data;
                    }

                    return null;
                }
            } finally {
                _tasks.Remove(url);
            }
        }

        [ItemCanBeNull]
        private async Task<string> GetStringAsyncInner([NotNull] string url, string cacheKey = null, TimeSpan? aliveTime = null,
                CancellationToken cancellation = default(CancellationToken)) {
            try {
                var bytes = await GetBytesAsync(url, cacheKey, aliveTime, cancellation);
                if (bytes == null || cancellation.IsCancellationRequested) return null;
                return await Task.Run(() => bytes.ToUtf8String());
            }finally {
                _stringTasks.Remove(url);
            }
        }

        private readonly Dictionary<string, Task<byte[]>> _tasks = new Dictionary<string, Task<byte[]>>(5);
        private readonly Dictionary<string, Task<string>> _stringTasks = new Dictionary<string, Task<string>>(5);

        [ItemCanBeNull]
        public Task<byte[]> GetBytesAsync([NotNull] string url, string cacheKey = null, TimeSpan? aliveTime = null,
                CancellationToken cancellation = default(CancellationToken)) {
            if (_tasks.TryGetValue(url, out var s)) return s;
            return _tasks[url] = GetBytesAsyncInner(url, cacheKey, aliveTime, cancellation);
        }

        [ItemCanBeNull]
        public Task<string> GetStringAsync([NotNull] string url, string cacheKey = null, TimeSpan? aliveTime = null,
                CancellationToken cancellation = default(CancellationToken)) {
            if (_stringTasks.TryGetValue(url, out var s)) return s;
            return _stringTasks[url] = GetStringAsyncInner(url, cacheKey, aliveTime, cancellation);
        }
    }
}