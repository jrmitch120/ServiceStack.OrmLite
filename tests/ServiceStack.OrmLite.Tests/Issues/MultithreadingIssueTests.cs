﻿using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using ServiceStack.Text;

namespace ServiceStack.OrmLite.Tests.Issues
{
    [Explicit]
    [TestFixture]
    public class MultithreadingIssueTests
        : OrmLiteTestBase
    {
        [SetUp]
        public void SetUp()
        {
            using (var db = OpenDbConnection())
            {
                db.DropAndCreateTable<ModelWithDifferentNumTypes>();
            }
        }

        [Test]
        public void Can_SaveAll_in_multiple_threads()
        {
            var errors = new List<string>();
            int threads = 0;
            var mainLock = new object();

            10.Times(() => {
                ThreadPool.QueueUserWorkItem(_ => {

                    Interlocked.Increment(ref threads);
                    var threadId = Thread.CurrentThread.ManagedThreadId;
                    "Thread {0} started...".Print(threadId);
                    
                    var rows = 10.Times(i => ModelWithDifferentNumTypes.Create(i));
                    using (var db = OpenDbConnection())
                    {
                        100.Times(i =>
                        {
                            try
                            {
                                db.SaveAll(rows);
                            }
                            catch (Exception ex)
                            {
                                lock (errors)
                                    errors.Add(ex.Message + "\nStackTrace:\n" + ex.StackTrace);
                            }
                        });
                    }

                    "Thread {0} finished".Print(threadId);
                    if (Interlocked.Decrement(ref threads) == 0)
                    {
                        "All Threads Finished".Print();
                        lock (mainLock)
                            Monitor.Pulse(mainLock);
                    }
                });
            });

            lock (mainLock)
                Monitor.Wait(mainLock);

            "Stopping...".Print();
            errors.PrintDump();

            Assert.That(errors.Count, Is.EqualTo(0));
        }
    }
}