using System;
using System.Linq;
using NUnit.Framework;

namespace battleships
{
	public class MapGenerator
	{
		private readonly int height;
		private readonly Random random;
		private readonly int[] shipSizes;
		private readonly int width;

		public MapGenerator(int width, int height, int[] shipSizes, Random random)
		{
			this.width = width;
			this.height = height;
			this.shipSizes = shipSizes.OrderByDescending(s => s).ToArray();
			this.random = random;
		}

		public Map GenerateMap()
		{
			var map = new Map(width, height);
			foreach (var size in shipSizes)
				PlaceShip(map, size);
			return map;
		}

		private void PlaceShip(Map map, int size)
		{
			var cells = Vector.Rect(0, 0, width, height).OrderBy(v => random.Next());
			foreach (var loc in cells)
			{
				var direction = random.Next(2) == 0 ? Direction.Horizontal : Direction.Vertical;
				if (map.SetShip(loc, size, direction)) return;

				direction = direction == Direction.Horizontal ? Direction.Vertical : Direction.Horizontal;
				if (map.SetShip(loc, size, direction)) return;
			}
			throw new Exception("Can't put next ship on map. No free space");
		}
	}

	[TestFixture]
	public class MapGenerator_should
	{
		[Test]
		public void always_succeed_on_standard_map()
		{
			var gen = new MapGenerator(10, 10, new[] {1, 1, 1, 1, 2, 2, 2, 3, 3, 4}, new Random());
			for (var i = 0; i < 10000; i++)
				gen.GenerateMap();
		}
	}
}