using System.Text;

public class PathBuilder
{
	private StringBuilder _pathBuilder = new StringBuilder();

	public PathBuilder GoDown(string nodeId)
	{
		if(_pathBuilder.Length > 0)
		{
			_pathBuilder.Append("/");
		}
		_pathBuilder.Append(nodeId);
		return this;
	}

	public PathBuilder GoUp()
	{
		if(_pathBuilder.Length > 0)
		{
			_pathBuilder.Append("/");
		}
		_pathBuilder.Append("..");
		return this;
	}

	public PathBuilder Clear()
	{
		_pathBuilder.Clear();
		return this;
	}

	public PathBuilder Result(out string result)
	{
		result = _pathBuilder.ToString();
		return this;
	}
}
