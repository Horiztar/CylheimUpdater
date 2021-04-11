using System;
using System.Text;

namespace CylheimUpdater
{
    public static class ExceptionUtil
    {

        public static string GetDetail(this Exception e)
        {
            StringBuilder stringBuilder = new StringBuilder();

            if (e.InnerException != null)
            {
                stringBuilder.Append(e.InnerException.GetDetail());
                stringBuilder.Append("\n\n");
            }

            stringBuilder.Append(e.Message);
            stringBuilder.Append("\n");
            stringBuilder.Append(e.StackTrace);

            return stringBuilder.ToString();
        }
    }
}