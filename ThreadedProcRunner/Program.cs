using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreadedProcRunner
{
    using System;
    using System.Threading;
    using System.Diagnostics;
    using System.Collections.Generic;


        class Runner
        {


            static void Main(string[] args)
            {
                List<string> inpList = new List<string>();
                object[] _lstOutput1=new object[1];
                object[] _lstOutpput2;
                TimeSpan _timespan1, _timespan2;
                Stopwatch _stopwatch = new Stopwatch();
                System.Timers.Timer _refreshTimer= new System.Timers.Timer(200);
                ConsoleManager _consoleManager = ConsoleManager.Instance("Process Runner",500); 

                for (int i = 0; i < 6000; i++)
                {
                    inpList.Add("hey there" + i.ToString());
                }

                TestProcClass instance = new TestProcClass();

                #region Sequential
                _consoleManager.add("Started Processing List Sequentially:");
                _stopwatch.Start();
                try
                {
                    _refreshTimer.Elapsed += new System.Timers.ElapsedEventHandler((sender, e) =>
                    {
                        _consoleManager.add(string.Format("Processing {0} item.", instance.CurrentRecord));
                    });
                    _refreshTimer.Start();
                    _lstOutput1 = instance.PerformAction(inpList.ToArray());
                    _refreshTimer.Stop();
                }
                catch (Exception exc)
                {
                    _consoleManager.add(exc.Message);
                }

                _stopwatch.Stop();
                _timespan1 = _stopwatch.Elapsed;
                _consoleManager.add(string.Format("Processed sequentially! {0} output items took {1} miliseconds.", _lstOutput1.Length, _timespan1.TotalMilliseconds)); 
                #endregion Sequential


                #region Threaded
                _consoleManager.add("Started Processing List in 5 threads - query batch size of 200:");
                _stopwatch.Reset();
                _stopwatch.Start();
                using (var tpr = new ThreadedProcRunner(5, 200))
                {
                    _refreshTimer.Elapsed += new System.Timers.ElapsedEventHandler((sender, e) =>
                    {
                        _consoleManager.add(string.Format("Processing {0} item.", tpr.CurrentRecord));
                    });
                    _refreshTimer.Start();
                    _lstOutpput2 = tpr.Execute(instance.PerformAction, inpList.ToArray() as object[]);
                    _refreshTimer.Stop();
                    int ercount = 0;
                    if (tpr.ErrorCount > 0)
                    {
                        tpr.Exceptions.ForEach((x) => { ercount++; _consoleManager.add("[Error: " + ercount + "] =>" + x.Message); });
                    }
                }
                _stopwatch.Stop();
                _timespan2 = _stopwatch.Elapsed;
                _consoleManager.add(string.Format("Processed on 5 threads (query batch size 200)! {0} output items took {1} miliseconds. \nPress any key to end.", _lstOutpput2.Length, _timespan2.TotalMilliseconds)); 
                #endregion Threaded

                Console.ReadKey();
            }

        }

        public class TestProcClass
        {
            public int CurrentRecord { get; private set; }
            public object[] PerformAction(object[] lstInput)
            {
                List<string> lstOp = new List<string>();

                for (int i = 0; i < lstInput.Length; i++)
                {
                    Thread.Sleep(1);
                    lstOp.Add("Processed Item: [" + i.ToString() + "] =>" + lstInput[i].ToString());
                    CurrentRecord = i + 1;
                }
                return (lstOp.ToArray());
            }
        }

        public class ThreadedProcRunner : IDisposable
        {


            public delegate object[] InvokerFunction(object[] inputParams);
            private List<Exception> _Exceptions;

            private int QUERY_BATCH_SIZE = 100;
            private int _MAX_THREAD_COUNT = 5;
            int CURRENT_THREAD_INDEX = 0;
            Thread[] _lstThread;
            ManualResetEvent[] _manualResetEvents;
            private object _resultLock;
            private object _errorLock;
            private List<object> results;
            private bool _disposed;
            private int _errorCount;
            public int CurrentRecord { get; private set; }

            public int ErrorCount
            {
                get { return _errorCount; }
            }


            public List<Exception> Exceptions
            {
                get { return _Exceptions; }
            }

            public ThreadedProcRunner(int MaxThreadCount= 5, int QueryBatchSize=100)
            {
                _MAX_THREAD_COUNT = (MaxThreadCount >= 0) ? MaxThreadCount : _MAX_THREAD_COUNT;
                QUERY_BATCH_SIZE = (QueryBatchSize > 0) ? QueryBatchSize : QUERY_BATCH_SIZE;
                _lstThread = new Thread[_MAX_THREAD_COUNT];
                _manualResetEvents = new ManualResetEvent[_MAX_THREAD_COUNT];
                _Exceptions = new List<Exception>();
                _resultLock = new Object();
                _errorLock = new Object();
                results = new List<object>();
            }

            public object[] Execute(InvokerFunction func, object[] inputParams)
            {

                List<object> queryBatch = new List<object>();

                for (int i = 0; i <= inputParams.Length - 1; i++)
                {
                    if (queryBatch.Count <= QUERY_BATCH_SIZE-1)
                    {
                        queryBatch.Add(inputParams[i]);
                    }

                    if ((queryBatch.Count >= QUERY_BATCH_SIZE) || (inputParams.Length == i+1))
                    {
                        object[] bat = queryBatch.ToArray();
                        processQueryBatch(bat, func);
                        queryBatch = new List<object>();
                    }
                    CurrentRecord = i + 1;
                }

                Dispose(true);
                return (results.ToArray());
            }

            protected virtual void Dispose(bool disposing)
            {
                if (!_disposed)
                {
                    if (disposing)
                    {
                        WaitAll();
                    }

                    _disposed = true;
                }
            }

            private void processQueryBatch(object[] queryBatch, InvokerFunction func)
            {
                if (CURRENT_THREAD_INDEX <= _MAX_THREAD_COUNT - 1)
                {
                    Thread procThread = new Thread(new ParameterizedThreadStart((qb) => {
                        try
                        {
                            AddToResults(func(qb as object[]));
                        }
                        catch (Exception exc) {
                            _errorCount++;
                            lock (_errorLock)
                            {
                                _Exceptions.Add(exc);
                            }
                        }
                    }));
                    procThread.Name = "[Fetch Thread " + CURRENT_THREAD_INDEX + "]";
                    procThread.Start(queryBatch);
                    _lstThread[CURRENT_THREAD_INDEX] = procThread;

                    CURRENT_THREAD_INDEX++;
                }

                //restart the thread processing loop
                else
                {
                    WaitAll();

                    //then proceed further
                    CURRENT_THREAD_INDEX = 0;
                    processQueryBatch(queryBatch, func);
                }
            }

            private void WaitAll()
            {
                for (int i = 0; i < _lstThread.Length; i++)
                {
                    if((_lstThread[i]!=null)&&(_lstThread[i].IsAlive))
                    _lstThread[i].Join();
                }
            }

            private void AddToResults(object[] p)
            {
                lock (_resultLock)
                {
                    foreach (object rslt in p)
                    {
                        results.Add(rslt);
                    }
                }
            }

            public void Dispose()
            {
                Dispose(true);
            }

        }
    }
