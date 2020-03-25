namespace Demon.PDF
{
	/// <summary>
	/// An object that implements this interface can be included in the
	/// generator's collection of indirect objects, and can be packaged
	/// in an ObjectReference.
	/// </summary>
	public interface IIndirectObject
	{
	}

	public class ObjectReference
	{
		private IIndirectObject _object;
		private int _number;
		private int _generation;
		private string _id;
		private string _reference;

		public IIndirectObject Object { get { return _object; } }
		public int Number { get { return _number; } }
		public int Generation { get { return _generation; } }
		public string Id { get { return _id; } }
		public string Reference { get { return _reference; } }
		public bool InUse { get; set; }
		public long ByteOffset { get; set; }

		public ObjectReference(IIndirectObject obj, int number)
		{
			_object = obj;
			_number = number;

			//	We'll fill in the byte offset when we know it
			ByteOffset = 0;

			//	We don't support updating the document, so all objects
			//	are in-use and are at generation zero. But we implement
			//	the properties anyway for semantic clarity.
			_generation = 0;
			InUse = true;

			_id = $"{_number} {_generation}";
			_reference = $"{_number} {_generation} R";
		}
	}
}
