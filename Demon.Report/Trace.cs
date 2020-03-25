using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Demon.Report.Types;

namespace Demon.Report
{
	internal class TraceContextStack
	{
		private Stack<TraceContext> _stack = new Stack<TraceContext>();
		private object _lock = new object();

		public TraceContextStack(int trackingId, bool traceLayout, bool traceText, bool tracePath, bool traceOutline)
		{
			TraceContext context = new TraceContext
			{
				OwnerTrackingId = trackingId,
				TraceLayout = traceLayout,
				TraceText = traceText,
				TracePath = tracePath,
				TraceOutline = traceOutline
			};
			_stack.Push(context);
		}

		public void Push(TraceContext context)
		{
			lock(_lock)
			{
				//	Merge the incoming context with the current context
				TraceContext top = _stack.Peek();
				TraceContext merged = new TraceContext
				{
					OwnerTrackingId = context.OwnerTrackingId,
					TraceLayout  = top.TraceLayout  || context.TraceLayout,
					TraceText    = top.TraceText    || context.TraceText,
					TracePath    = top.TracePath    || context.TracePath,
					TraceOutline = top.TraceOutline || context.TraceOutline
				};
				_stack.Push(merged);
			}
		}

		public void Pop()
		{
			lock(_lock)
			{
				_stack.Pop();
			}
		}

		public bool TraceLayout
		{
			get
			{
				lock(_lock)
				{
					return _stack.Peek().TraceLayout;
				}
			}
		}

		public bool TraceText
		{
			get
			{
				lock(_lock)
				{
					return _stack.Peek().TraceText;
				}
			}
		}

		public bool TracePath
		{
			get
			{
				lock(_lock)
				{
					return _stack.Peek().TracePath;
				}
			}
		}

		public bool TraceOutline
		{
			get
			{
				lock(_lock)
				{
					return _stack.Peek().TraceOutline;
				}
			}
		}
	}

	/// <summary>
	/// Pushes a trace context onto the generator's stack and then pops it
	/// when the pusher is disposed.
	/// </summary>
	internal class TraceContextPusher : IDisposable
	{
		private Generator _generator;
		public TraceContextPusher(Generator generator, TraceContext context)
		{
			_generator = generator;
			_generator.PushTraceContext(context);
		}

		~TraceContextPusher()
		{
			Dispose(false);
		}

		public void Dispose()
		{
			Dispose(true);
		}

		private void Dispose(bool disposing)
		{
			if(disposing)
				_generator.PopTraceContext();
		}
	}
}
