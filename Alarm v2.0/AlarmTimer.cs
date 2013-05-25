using System;
using System.Timers;

namespace Alarm_v2._0
{
    public class AlarmTimer
    {
        private DateTime _alarm;
        private Timer _t;

        public AlarmTimer(DateTime alarmTime)
        {
            _alarm = alarmTime;
        }

        public event EventHandler AlarmHit;

        public void Start()
        {
            _t = new Timer();
            _t.Elapsed += Ticked;
            _t.Interval = 1000;
            _t.Start();
        }

        public void Exit()
        {
            if (_t != null)
                _t.Dispose();
        }

        private void Ticked(object sender, ElapsedEventArgs e)
        {
            if (_alarm.ToShortTimeString().Equals(DateTime.Now.ToShortTimeString()))
            {
                _t.Stop();
                //http://msdn.microsoft.com/en-us/library/9aackb16.aspx#Y1789
                OnAlarmHit(EventArgs.Empty);
            }
        }

        protected virtual void OnAlarmHit(EventArgs e)
        {
            EventHandler handler = AlarmHit;
            if (handler != null)
                handler(this, e);
        }
    }
}