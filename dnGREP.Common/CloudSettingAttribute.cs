using System;

namespace dnGREP.Common
{
    [AttributeUsage(AttributeTargets.Field)]
    public class CloudSettingAttribute : Attribute
    {
        public CloudSettingAttribute() { }
    }
}
