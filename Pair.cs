using System;
using System.Collections.Generic;

namespace mqtt
{
	[Serializable]
	public class Pair<F, S>
	{
		public Pair(F first, S second)
		{
			this.first = first;
			this.second = second;
		}

		public F First
		{
			get
			{
				return first;
			}

			set
			{
				first = value;
			}
		}

		public S Second
		{
			get
			{
				return second;
			}

			set
			{
				second = value;
			}
		}

		public bool Equals(Pair<F, S> other)
		{
			return first.Equals(other.first) && second.Equals(other.second);
		}

		F first;
		S second;
	}

	public class PairComparer : IEqualityComparer<Pair<int, int>>
	{
		public bool Equals(Pair<int, int> left, Pair<int, int> right)
		{
			return left.Equals(right);
		}

		public int GetHashCode(Pair<int, int> o)
		{
			return o.First * o.Second;
		}
	}
}
