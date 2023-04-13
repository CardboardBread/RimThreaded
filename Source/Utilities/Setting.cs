using System;

namespace RimThreaded.Utilities
{
    public struct Setting<T>
    {
        public static implicit operator T(Setting<T> setting) => setting.GetValue();

        internal T Value = default;
        internal string EditBuffer = null;
        internal readonly Func<T, T> getFilter;
        internal readonly Func<T, T> setFilter;

        public Setting(Func<T, T> filter)
        {
            getFilter = filter;
            setFilter = filter;
        }

        public Setting(Func<T, T> getFilter, Func<T, T> setFilter)
        {
            this.getFilter = getFilter;
            this.setFilter = setFilter;
        }

        public T GetValue() => getFilter != null ? getFilter(Value) : Value;

        public void SetValue(T value) => Value = setFilter != null ? setFilter(value) : value;
    }
}

