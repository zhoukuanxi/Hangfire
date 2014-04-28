// This file is part of HangFire.
// Copyright � 2013-2014 Sergey Odinokov.
// 
// HangFire is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as 
// published by the Free Software Foundation, either version 3 
// of the License, or any later version.
// 
// HangFire is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public 
// License along with HangFire. If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using HangFire.Common.States;
using HangFire.Server;
using HangFire.States;
using HangFire.Storage;
using HangFire.Storage.Monitoring;
using ServiceStack.Redis;

namespace HangFire.Redis
{
    public class RedisStorage : JobStorage
    {
        internal static readonly string Prefix = "hangfire:";

        private readonly PooledRedisClientManager _pooledManager;

        public RedisStorage()
            : this(String.Format("{0}:{1}", RedisNativeClient.DefaultHost, RedisNativeClient.DefaultPort))
        {
        }

        public RedisStorage(string hostAndPort)
            : this(hostAndPort, (int)RedisNativeClient.DefaultDb)
        {
        }

        public RedisStorage(string hostAndPort, int db)
            : this(hostAndPort, db, new RedisStorageOptions())
        {
        }

        public RedisStorage(string hostAndPort, int db, RedisStorageOptions options)
        {
            HostAndPort = hostAndPort;
            Db = db;
            Options = options;

            _pooledManager = new PooledRedisClientManager(
                new []{ HostAndPort },
                new string[0],
                new RedisClientManagerConfig
                {
                    DefaultDb = Db,
                    MaxWritePoolSize = Options.ConnectionPoolSize
                });
        }

        public string HostAndPort { get; private set; }
        public int Db { get; private set; }
        public RedisStorageOptions Options { get; private set; }

        public PooledRedisClientManager PooledManager { get { return _pooledManager; } }

        public override IMonitoringApi GetMonitoringApi()
        {
            return new RedisMonitoringApi(_pooledManager.GetClient());
        }

        public override IStorageConnection GetConnection()
        {
            return new RedisConnection(_pooledManager.GetClient());
        }

        public override IEnumerable<IServerComponent> GetComponents()
        {
            var stateMachineFactory = new StateMachineFactory(this);

            yield return new SchedulePoller(this, stateMachineFactory, Options.PollInterval);
            yield return new FetchedJobsWatcher(this, stateMachineFactory);
        }

        public override IEnumerable<StateHandler> GetStateHandlers()
        {
            yield return new FailedStateHandler();
            yield return new ProcessingStateHandler();
            yield return new SucceededStateHandler();
        }

        public override string ToString()
        {
            return String.Format("redis://{0}/{1}", HostAndPort, Db);
        }
    }
}