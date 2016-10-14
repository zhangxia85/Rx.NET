﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information. 

#if !NO_PERF
using System;
using System.Reactive.Disposables;
using System.Threading;

namespace System.Reactive.Linq.ObservableImpl
{
    class WithLatestFrom<TFirst, TSecond, TResult> : Producer<TResult>
    {
        private readonly IObservable<TFirst> _first;
        private readonly IObservable<TSecond> _second;
        private readonly Func<TFirst, TSecond, TResult> _resultSelector;

        public WithLatestFrom(IObservable<TFirst> first, IObservable<TSecond> second, Func<TFirst, TSecond, TResult> resultSelector)
        {
            _first = first;
            _second = second;
            _resultSelector = resultSelector;
        }

        protected override IDisposable Run(IObserver<TResult> observer, IDisposable cancel, Action<IDisposable> setSink)
        {
            var sink = new _(this, observer, cancel);
            setSink(sink);
            return sink.Run();
        }

        class _ : Sink<TResult>
        {
            private readonly WithLatestFrom<TFirst, TSecond, TResult> _parent;

            public _(WithLatestFrom<TFirst, TSecond, TResult> parent, IObserver<TResult> observer, IDisposable cancel)
                : base(observer, cancel)
            {
                _parent = parent;
            }

            private object _gate;
            private volatile bool _hasLatest;
            private TSecond _latest;

            private object _latestGate;

            public IDisposable Run()
            {
                _gate = new object();
                _latestGate = new object();

                var sndSubscription = new SingleAssignmentDisposable();

                var fstO = new F(this);
                var sndO = new S(this, sndSubscription);

                var fstSubscription = _parent._first.SubscribeSafe(fstO);
                sndSubscription.Disposable = _parent._second.SubscribeSafe(sndO);

                return StableCompositeDisposable.Create(fstSubscription, sndSubscription);
            }

            class F : IObserver<TFirst>
            {
                private readonly _ _parent;

                public F(_ parent)
                {
                    _parent = parent;
                }

                public void OnCompleted()
                {
                    lock (_parent._gate)
                    {
                        _parent._observer.OnCompleted();
                        _parent.Dispose();
                    }
                }

                public void OnError(Exception error)
                {
                    lock (_parent._gate)
                    {
                        _parent._observer.OnError(error);
                        _parent.Dispose();
                    }
                }

                public void OnNext(TFirst value)
                {
                    if (_parent._hasLatest) // Volatile read
                    {

                        TSecond latest;

                        lock (_parent._latestGate)
                        {
                            latest = _parent._latest;
                        }

                        var res = default(TResult);

                        try
                        {
                            res = _parent._parent._resultSelector(value, latest);
                        }
                        catch (Exception ex)
                        {
                            lock (_parent._gate)
                            {
                                _parent._observer.OnError(ex);
                                _parent.Dispose();
                            }

                            return;
                        }

                        lock (_parent._gate)
                        {
                            _parent._observer.OnNext(res);
                        }
                    }
                }
            }

            class S : IObserver<TSecond>
            {
                private readonly _ _parent;
                private readonly IDisposable _self;

                public S(_ parent, IDisposable self)
                {
                    _parent = parent;
                    _self = self;
                }

                public void OnCompleted()
                {
                    _self.Dispose();
                }

                public void OnError(Exception error)
                {
                    lock (_parent._gate)
                    {
                        _parent._observer.OnError(error);
                        _parent.Dispose();
                    }
                }

                public void OnNext(TSecond value)
                {
                    lock (_parent._latestGate)
                    {
                        _parent._latest = value;
                    }

                    if (!_parent._hasLatest)
                    {
                        _parent._hasLatest = true;
                    }
                }
            }
        }
    }
}
#endif
