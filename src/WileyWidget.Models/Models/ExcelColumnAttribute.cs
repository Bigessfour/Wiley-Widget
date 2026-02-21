using System;

namespace WileyWidget.Models
{
    [AttributeUsage(AttributeTargets.Property)]
    public class ExcelColumnAttribute : Attribute
    {
        public string Name { get; }
        public int Order { get; }
        public string? Format { get; }
        public bool IsTotaled { get; }

        public ExcelColumnAttribute(string name, int order = 0, string? format = null, bool isTotaled = false)
        {
            Name = name;
            Order = order;
            Format = format;
            IsTotaled = isTotaled;
        }
    }
}
