using System.Reflection;

namespace FuziotDB
{
    internal struct FieldInfo
    {
        public Field field;
        public System.Reflection.FieldInfo info;

        public FieldInfo(Field field, System.Reflection.FieldInfo info)
        {
            this.field = field;
            this.info = info;
        }
    }
}