namespace Demon.Path
{
	/// <summary>
	/// A navigation is a direction and a distance limit.
	/// </summary>
	internal class Navigation
	{
		private Direction _direction;
		private int _limit; // 0 = no limit

		internal Direction Direction { get { return _direction; }}
		internal int Limit { get { return _limit; }}

		internal Navigation(string text, ref int pos, Origin origin)
		{
			//	[+-]?(?[0-9]+:)
			//	Optional +/- to indicate direction, default is down
			//	Optional integer limit followed by colon separator

			switch(text[pos])
			{
				case '+':
				{
					_direction = Direction.Down;
					++pos;
					break;
				}
				case '-':
				{
					_direction = Direction.Up;
					++pos;
					break;
				}
			}

			//	Take a note of whether the spec indicates a limit,
			//	so that we can use the default if not. Note that
			//	the limit must be zero to start this loop, because
			//	the starting value is multiplied by ten at the
			//	first iteration, so we can't set the default until
			//	after the loop.
			bool useDefault = true;
			while(pos < text.Length)
			{
				char c = text[pos];
				if(c >= '0' && c <= '9')
				{
					useDefault = false;
					int digit = (int)(c - '0');
					_limit *= 10;
					_limit += digit;
					++pos;
				}
				else if(c == ':')
				{
					++pos;
					break;
				}
				else
				{
					//	End of the limit, regardless of what comes next
					break;
				}
			}
			if(useDefault)
				_limit = 0;
		}

		internal Navigation()
		{
			_direction = Direction.Down;
			_limit = 1;
		}

		internal Navigation(Navigation other)
		{
			_direction = other._direction;
			_limit = other._limit;
		}

		public override string ToString()
		{
			string s = "";
			switch(_direction)
			{
				case Direction.Up:
					s += "-";
					break;
				case Direction.Down:
					s += "+";
					break;
			}
			s += _limit.ToString("D");
			return s;
		}
	}

	internal enum Direction
	{
		Down = 0, // default
		Up   = 1
	}
}
