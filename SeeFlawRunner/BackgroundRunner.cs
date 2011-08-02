using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace SeeFlawRunner
{
    public class BackgroundRunner : BackgroundWorker
    {
        private bool killProcess = false;

        public void Reset()
        {
            killProcess = false;
        }

        public void KillAsync()
        {
            killProcess = true;
        }

        public bool KillPending
        {
            get
            {
                return killProcess;
            }
        }
    }
}
