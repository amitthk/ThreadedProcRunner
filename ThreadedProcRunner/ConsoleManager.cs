using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ThreadedProcRunner
{
    public class ConsoleManager
    {
        object _msgLock = new Object();
        private string _Name;
        private List<string> _lstMessages;
        private bool _isDirty=false;
        System.Timers.Timer _refreshTimer;

        
        public static ConsoleManager Instance(string Name,int RefreshRateInMilliSeconds)
	    {
            ConsoleManager instance = new ConsoleManager(Name,RefreshRateInMilliSeconds);
            return (instance);
	    }

        public void add(string message)
        {
            CurrentMessage = message;

        }

        private ConsoleManager(string name,int RefreshRateInMilliSeconds)
        {
            _Name = name;
            _lstMessages = new List<string>();
            _refreshTimer= new System.Timers.Timer(RefreshRateInMilliSeconds);
            _refreshTimer.Elapsed += new System.Timers.ElapsedEventHandler((sender, e) =>
            {
                this.refresh();
            });
            _refreshTimer.Start();
        }

        private string CurrentMessage
        {
            get
            {
                lock (_msgLock)
                {
                    string ltstMsgs = string.Join("\n", _lstMessages.ToArray());
                    _lstMessages.Clear();
                    _isDirty = false;
                    return (ltstMsgs);
                }
            }
            set
            {
                lock (_msgLock)
                {
                    if (!_lstMessages.Exists(x => x.Equals(value, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        string msg = string.Format("[{0} {1}] : {2}", DateTime.Now.ToString("HH:mm:ss"),_Name, value);
                        _lstMessages.Add(msg);
                        _isDirty = true;
                    }
                }
            }
        }

        private void refresh()
        {
            if (_isDirty)
            {
                Console.WriteLine(CurrentMessage);
            }
        }

    }
}
