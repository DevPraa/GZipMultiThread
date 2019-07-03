using System;
using System.Collections.Generic;
using System.Text;

namespace GZipUtils.Utils
{
    public static class Ext
    {
        static object LockObj = new object();
        public static byte[] Skip(byte[] Mass, int number)
        {
            lock (LockObj)
            {
                byte[] tmpMass = new byte[Mass.Length - number];
                for (int i = number; i < Mass.Length; i++)
                {
                    tmpMass[i - number] = Mass[i];
                }
                return tmpMass;
            }
        }
    }
}
