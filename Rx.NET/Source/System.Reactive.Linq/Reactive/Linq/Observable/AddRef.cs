﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information. 

#if !NO_PERF
using System;
using System.Reactive.Disposables;

namespace System.Reactive.Linq.ObservableImpl
{
    class AddRef<TSource> : Producer<TSource>
    {
        private readonly IObservable<TSource> _source;
        private readonly RefCountDisposable _refCount;

        public AddRef(IObservable<TSource> source, RefCountDisposable refCount)
        {
            _source = source;
            _refCount = refCount;
        }

        protected override IDisposable Run(IObserver<TSource> observer, IDisposable cancel, Action<IDisposable> setSink)
        {
            var d = StableCompositeDisposable.Create(_refCount.GetDisposable(), cancel);

            var sink = new _(observer, d);
            setSink(sink);
            return _source.SubscribeSafe(sink);
        }

        class _ : Sink<TSource>, IObserver<TSource>
        {
            public _(IObserver<TSource> observer, IDisposable cancel)
                : base(observer, cancel)
            {
            }

            public void OnNext(TSource value)
            {
                base._observer.OnNext(value);
            }

            public void OnError(Exception error)
            {
                base._observer.OnError(error);
                base.Dispose();
            }

            public void OnCompleted()
            {
                base._observer.OnCompleted();
                base.Dispose();
            }
        }
    }
}
#endif