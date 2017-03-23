using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sharp80
{
    public class SubArray<T> : IReadOnlyList<T>
    {
        private T[] BaseArray;
        private int Start;
        private int End;

        public SubArray(T[] BaseArray, int Start, int End)
        {
            this.BaseArray = BaseArray;
            this.Start = Start;
            this.End = End;
        }
        public T this[int Index] => BaseArray[Index + Start];
        public int Count => End - Start;

        public IEnumerator<T> GetEnumerator()
        {
            int i = Start;
            if (i < End)
                yield return BaseArray[i++];
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
