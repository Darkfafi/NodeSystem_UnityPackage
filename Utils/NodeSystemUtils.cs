public static class NodeSystemUtils
{
	public static int CycleIndex(int index, int length)
	{
		if(length == 0)
		{
			return -1;
		}

		index = index % length;
		if(index < 0)
		{
			index = length + index;
		}
		return index;
	}
}
