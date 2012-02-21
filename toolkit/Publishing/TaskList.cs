namespace CoApp.Developer.Toolkit.Publishing {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /*
   public class AsyncLazy<T> : Lazy<Task<T>> {
       public AsyncLazy(Func<T> valueFactory) :
           base(() => Task.Factory.StartNew(valueFactory)) { }

       public AsyncLazy(Func<Task<T>> taskFactory) :
           base(() => Task.Factory.StartNew(taskFactory).Unwrap()) { }

       public T SyncValue {
           get { return Value.Result; }
       }

       // public TaskAwaiter GetAwaiter() { return Value.GetAwaiter(); } // .net 4.5 
   }
   */

    public class TaskList : List<Task> {
        public Task Start(Action action) {
            var task = Task.Factory.StartNew(action);
            lock (this) {
                Add(task);
            }
            return task;
        }

        public Task<T> Start<T>(Func<T> func) {
            var task = Task<T>.Factory.StartNew(func);
            lock (this) {
                Add(task);
            }
            return task;
        }

        /// <summary>
        /// Waits for all the tasks in the collection (even if the collection changes during the wait)
        /// </summary>
        public void WaitAll() {
            do {
                Task.WaitAll(ToArray());
                                
                if (Task.WaitAll(ToArray(), 0)) {
                    // if they still look done, we're done.
                    return;
                }
            } while (true);
        }
        public void WaitAll(CancellationToken cancellationToken) {
            do {
                Task.WaitAll(ToArray(), cancellationToken);

                if (cancellationToken.IsCancellationRequested || Task.WaitAll(ToArray(), 0)) {
                    // if they still look done, we're done.
                    return;
                }
            } while (true);
        }
        public bool WaitAll(int millisecondsTimeout) {
            for(var i=0;i< millisecondsTimeout/100;i++) {
                if (!Task.WaitAll(ToArray(), 100)) {
                    // timed out, try again
                    continue;
                }
                
                if (Task.WaitAll(ToArray(), 0)) {
                    // if they still look done, we're done.
                    return true;
                }
            }
            return false;
        }

        public bool WaitAll(int millisecondsTimeout, CancellationToken cancellationToken) {
            for (var i = 0; i < millisecondsTimeout/100; i++) {
                if (!Task.WaitAll(ToArray(), 100, cancellationToken)) {
                    // timed out, try again
                    continue;
                }
                if (cancellationToken.IsCancellationRequested || Task.WaitAll(ToArray(), 0)) {
                    // if they still look done, we're done.
                    return true;
                }
            }
            return false;
        }

        public Task ContinueWhenAll(Action<Task[]> continuationAction) {
            return Task.Factory.ContinueWhenAll(ToArray(), (tasks) => {
                if (tasks.SequenceEqual(this)) {
                    continuationAction(tasks);
                } else {
                    // uh, try again...
                    ContinueWhenAll(continuationAction, TaskContinuationOptions.AttachedToParent);
                }
            });
        }

        public Task ContinueWhenAll(Action<Task[]> continuationAction, TaskContinuationOptions continuationOptions) {
            return Task.Factory.ContinueWhenAll(ToArray(), (tasks) => {
                if (tasks.SequenceEqual(this)) {
                    continuationAction(tasks);
                } else {
                    // uh, try again...
                    ContinueWhenAll(continuationAction, continuationOptions & TaskContinuationOptions.AttachedToParent);
                }
            });
        }
    }
}