﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

#if HAS_AWAIT

using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using Microsoft.Reactive.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReactiveTests.Dummies;
using System.Reactive.Disposables;

namespace ReactiveTests.Tests
{
    [TestClass]
    public class ObservableAwaiterTest : ReactiveTest
    {
        [TestMethod]
        public void Await_ArgumentChecking()
        {
            ReactiveAssert.Throws<ArgumentNullException>(() => Observable.GetAwaiter<int>(default(IObservable<int>)));
            ReactiveAssert.Throws<ArgumentNullException>(() => Observable.GetAwaiter<int>(default(IConnectableObservable<int>)));

            ReactiveAssert.Throws<ArgumentNullException>(() => Observable.GetAwaiter(Observable.Empty<int>()).OnCompleted(null));
        }

        [TestMethod]
        public void Await()
        {
            var scheduler = new TestScheduler();

            var xs = scheduler.CreateHotObservable(
                OnNext(20, -1),
                OnNext(150, 0),
                OnNext(220, 1),
                OnNext(290, 2),
                OnNext(340, 3),
                OnCompleted<int>(410)
            );

            var awaiter = default(AsyncSubject<int>);
            var result = default(int);
            var t = long.MaxValue;

            scheduler.ScheduleAbsolute(100, () => awaiter = xs.GetAwaiter());
            scheduler.ScheduleAbsolute(200, () => awaiter.OnCompleted(() => { t = scheduler.Clock; result = awaiter.GetResult(); }));

            scheduler.Start();

            Assert.AreEqual(410, t);
            Assert.AreEqual(3, result);

            xs.Subscriptions.AssertEqual(
                Subscribe(100)
            );
        }

        [TestMethod]
        public void Await_Connectable()
        {
            var scheduler = new TestScheduler();

            var s = default(long);

            var xs = Observable.Create<int>(observer =>
            {
                s = scheduler.Clock;

                return StableCompositeDisposable.Create(
                    scheduler.ScheduleAbsolute(250, () => { observer.OnNext(42); }),
                    scheduler.ScheduleAbsolute(260, () => { observer.OnCompleted(); })
                );
            });

            var ys = xs.Publish();

            var awaiter = default(AsyncSubject<int>);
            var result = default(int);
            var t = long.MaxValue;

            scheduler.ScheduleAbsolute(100, () => awaiter = ys.GetAwaiter());
            scheduler.ScheduleAbsolute(200, () => awaiter.OnCompleted(() => { t = scheduler.Clock; result = awaiter.GetResult(); }));

            scheduler.Start();

            Assert.AreEqual(100, s);
            Assert.AreEqual(260, t);
            Assert.AreEqual(42, result);
        }

        [TestMethod]
        public void Await_Error()
        {
            var scheduler = new TestScheduler();

            var ex = new Exception();

            var xs = scheduler.CreateHotObservable(
                OnNext(20, -1),
                OnNext(150, 0),
                OnNext(220, 1),
                OnNext(290, 2),
                OnNext(340, 3),
                OnError<int>(410, ex)
            );

            var awaiter = default(AsyncSubject<int>);
            var t = long.MaxValue;

            scheduler.ScheduleAbsolute(100, () => awaiter = xs.GetAwaiter());
            scheduler.ScheduleAbsolute(200, () => awaiter.OnCompleted(() => { t = scheduler.Clock; ReactiveAssert.Throws(ex, () => awaiter.GetResult()); }));

            scheduler.Start();

            Assert.AreEqual(410, t);

            xs.Subscriptions.AssertEqual(
                Subscribe(100)
            );
        }

        [TestMethod]
        public void Await_Never()
        {
            var scheduler = new TestScheduler();

            var xs = scheduler.CreateHotObservable(
                OnNext(20, -1),
                OnNext(150, 0),
                OnNext(220, 1),
                OnNext(290, 2),
                OnNext(340, 3)
            );

            var awaiter = default(AsyncSubject<int>);
            var hasValue = default(bool);
            var t = long.MaxValue;

            scheduler.ScheduleAbsolute(100, () => awaiter = xs.GetAwaiter());
            scheduler.ScheduleAbsolute(200, () => awaiter.OnCompleted(() => { t = scheduler.Clock; awaiter.GetResult(); hasValue = true; }));

            scheduler.Start();

            Assert.AreEqual(long.MaxValue, t);
            Assert.IsFalse(hasValue);

            xs.Subscriptions.AssertEqual(
                Subscribe(100)
            );
        }

        [TestMethod]
        public void Await_Empty()
        {
            var scheduler = new TestScheduler();

            var xs = scheduler.CreateHotObservable(
                OnCompleted<int>(300)
            );

            var awaiter = default(AsyncSubject<int>);
            var t = long.MaxValue;

            scheduler.ScheduleAbsolute(100, () => awaiter = xs.GetAwaiter());
            scheduler.ScheduleAbsolute(200, () => awaiter.OnCompleted(() => { t = scheduler.Clock; ReactiveAssert.Throws<InvalidOperationException>(() => awaiter.GetResult()); }));

            scheduler.Start();

            Assert.AreEqual(300, t);

            xs.Subscriptions.AssertEqual(
                Subscribe(100)
            );
        }

        [TestMethod]
        public void RunAsync_ArgumentChecking()
        {
            var ct = CancellationToken.None;

            ReactiveAssert.Throws<ArgumentNullException>(() => Observable.RunAsync<int>(default(IObservable<int>), ct));
            ReactiveAssert.Throws<ArgumentNullException>(() => Observable.RunAsync<int>(default(IConnectableObservable<int>), ct));
        }

        [TestMethod]
        public void RunAsync_Simple()
        {
            var scheduler = new TestScheduler();

            var xs = scheduler.CreateHotObservable(
                OnNext(220, 42),
                OnCompleted<int>(250)
            );

            var awaiter = default(AsyncSubject<int>);
            var result = default(int);
            var t = long.MaxValue;

            scheduler.ScheduleAbsolute(100, () => awaiter = xs.RunAsync(CancellationToken.None));
            scheduler.ScheduleAbsolute(200, () => awaiter.OnCompleted(() => { t = scheduler.Clock; result = awaiter.GetResult(); }));

            scheduler.Start();

            Assert.AreEqual(250, t);
            Assert.AreEqual(42, result);

            xs.Subscriptions.AssertEqual(
                Subscribe(100)
            );
        }

        [TestMethod]
        public void RunAsync_Cancelled()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();

            var scheduler = new TestScheduler();

            var xs = scheduler.CreateHotObservable(
                OnNext(220, 42),
                OnCompleted<int>(250)
            );

            var awaiter = default(AsyncSubject<int>);
            var result = default(int);
            var t = long.MaxValue;

            scheduler.ScheduleAbsolute(100, () => awaiter = xs.RunAsync(cts.Token));
            scheduler.ScheduleAbsolute(200, () => awaiter.OnCompleted(() =>
            {
                t = scheduler.Clock;

                ReactiveAssert.Throws<OperationCanceledException>(() =>
                {
                    result = awaiter.GetResult();
                });
            }));

            scheduler.Start();

            Assert.AreEqual(200, t);

            xs.Subscriptions.AssertEqual(
            );
        }

        [TestMethod]
        public void RunAsync_Cancel()
        {
            var cts = new CancellationTokenSource();

            var scheduler = new TestScheduler();

            var xs = scheduler.CreateHotObservable(
                OnNext(220, 42),
                OnCompleted<int>(250)
            );

            var awaiter = default(AsyncSubject<int>);
            var result = default(int);
            var t = long.MaxValue;

            scheduler.ScheduleAbsolute(100, () => awaiter = xs.RunAsync(cts.Token));
            scheduler.ScheduleAbsolute(200, () => awaiter.OnCompleted(() =>
            {
                t = scheduler.Clock;

                ReactiveAssert.Throws<OperationCanceledException>(() =>
                {
                    result = awaiter.GetResult();
                });
            }));
            scheduler.ScheduleAbsolute(210, () => cts.Cancel());

            scheduler.Start();

            Assert.AreEqual(210, t);

            xs.Subscriptions.AssertEqual(
                Subscribe(100, 210)
            );
        }

        [TestMethod]
        public void RunAsync_Connectable()
        {
            var scheduler = new TestScheduler();

            var s = default(long);

            var xs = Observable.Create<int>(observer =>
            {
                s = scheduler.Clock;

                return StableCompositeDisposable.Create(
                    scheduler.ScheduleAbsolute(250, () => { observer.OnNext(42); }),
                    scheduler.ScheduleAbsolute(260, () => { observer.OnCompleted(); })
                );
            });

            var ys = xs.Publish();

            var awaiter = default(AsyncSubject<int>);
            var result = default(int);
            var t = long.MaxValue;

            scheduler.ScheduleAbsolute(100, () => awaiter = ys.RunAsync(CancellationToken.None));
            scheduler.ScheduleAbsolute(200, () => awaiter.OnCompleted(() => { t = scheduler.Clock; result = awaiter.GetResult(); }));

            scheduler.Start();

            Assert.AreEqual(100, s);
            Assert.AreEqual(260, t);
            Assert.AreEqual(42, result);
        }

        [TestMethod]
        public void RunAsync_Connectable_Cancelled()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();

            var scheduler = new TestScheduler();

            var s = default(long?);

            var xs = Observable.Create<int>(observer =>
            {
                s = scheduler.Clock;

                return StableCompositeDisposable.Create(
                    scheduler.ScheduleAbsolute(250, () => { observer.OnNext(42); }),
                    scheduler.ScheduleAbsolute(260, () => { observer.OnCompleted(); })
                );
            });

            var ys = xs.Publish();

            var awaiter = default(AsyncSubject<int>);
            var result = default(int);
            var t = long.MaxValue;

            scheduler.ScheduleAbsolute(100, () => awaiter = ys.RunAsync(cts.Token));
            scheduler.ScheduleAbsolute(200, () => awaiter.OnCompleted(() =>
            {
                t = scheduler.Clock;

                ReactiveAssert.Throws<OperationCanceledException>(() =>
                {
                    result = awaiter.GetResult();
                });
            }));

            scheduler.Start();

            Assert.IsFalse(s.HasValue);
            Assert.AreEqual(200, t);
        }

        [TestMethod]
        public void RunAsync_Connectable_Cancel()
        {
            var cts = new CancellationTokenSource();

            var scheduler = new TestScheduler();

            var s = default(long);
            var d = default(long);

            var xs = Observable.Create<int>(observer =>
            {
                s = scheduler.Clock;

                return StableCompositeDisposable.Create(
                    scheduler.ScheduleAbsolute(250, () => { observer.OnNext(42); }),
                    scheduler.ScheduleAbsolute(260, () => { observer.OnCompleted(); }),
                    Disposable.Create(() => { d = scheduler.Clock; })
                );
            });

            var ys = xs.Publish();

            var awaiter = default(AsyncSubject<int>);
            var result = default(int);
            var t = long.MaxValue;

            scheduler.ScheduleAbsolute(100, () => awaiter = ys.RunAsync(cts.Token));
            scheduler.ScheduleAbsolute(200, () => awaiter.OnCompleted(() =>
            {
                t = scheduler.Clock;

                ReactiveAssert.Throws<OperationCanceledException>(() =>
                {
                    result = awaiter.GetResult();
                });
            }));
            scheduler.ScheduleAbsolute(210, () => cts.Cancel());

            scheduler.Start();

            Assert.AreEqual(100, s);
            Assert.AreEqual(210, d);
            Assert.AreEqual(210, t);
        }
    }
}

#endif
