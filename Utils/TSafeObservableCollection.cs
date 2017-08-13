using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
#if NETFRAMEWORK
using System.Windows.Threading;
#endif

namespace LocalCloudStorage
{
    public class TSafeObservableCollection<T> : ObservableCollection<T>
    {
        public override event NotifyCollectionChangedEventHandler CollectionChanged;
        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            /*
             * Source: "Loudenvier" https://stackoverflow.com/a/17451872/2972004
             *   For implementation that supports UI element updating
             * 
             */

            //don't send the event if there are no subscribers
            var handlers = CollectionChanged;
            if (handlers == null) return;

            //iterate through each registered handler
            foreach (var uncastedHandler in handlers.GetInvocationList())
            {
                //This is just because VS complains if we use "PropertyChangedEventHandler" instead of "var"
                var handler = uncastedHandler as NotifyCollectionChangedEventHandler;
                if (handler == null) continue;

                //Does this call need to be synchronized?
                var synch = handler.Target as ISynchronizeInvoke;
                if (synch != null && synch.InvokeRequired)
                {
                    //if so, then use the Invoke method
                    synch.Invoke(handler, new object[] { this, e });
                }
                else
                {
#if NETFRAMEWORK // only supported in the .net framework
                    //does it support dispatching?
                    var dispatcher = handler.Target as DispatcherObject;
                    if (dispatcher != null)
                    {
                        dispatcher.Dispatcher.Invoke(handler, new object[] {this, e});
                        return;
                    }
#endif
                    //otherwise, call it like a regular handler
                    handler(this, e);
                }
            }
        }
    }
}
