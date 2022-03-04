//using System;
//using System.Collections;
//using System.Collections.Generic;

//namespace Sharp80.TRS80
//{
//    /// <summary>
//    /// Used to expose a slice of an array as read only
//    /// </summary>
//    public class SubArray<T> : IReadOnlyList<T>
//    {
//        private T[] BaseArray;
//        private readonly int Start;
//        private readonly int End;

//        public int Count { get; private set; }

//        public SubArray(T[] BaseArray, int Start, int End)
//        {
//            this.BaseArray = BaseArray;
//            this.Start = Start;
//            this.End = End;

//            if (End < Start)
//                throw new Exception("Negative subarray length");

//            Count = End - Start;
//        }

//        public T this[int Index] => BaseArray[Index + Start];

//        public IEnumerator<T> GetEnumerator()
//        {
//            int i = Start;
//            while (i < End)
//                yield return BaseArray[i++];
//        }
//        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
//    }
//}
