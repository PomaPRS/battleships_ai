using System;
using System.Linq;
using NUnit.Framework;

namespace battleships
{
	public class MapGenerator
	{
		private readonly MyLogger logger = new MyLogger("log.txt");
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

			logger.Add("");
			logger.Add(string.Format("Generate Map {0} {1}", width, height));
			foreach (var ship in map.Ships)
			{
				logger.Add(ship.Location.ToString(), ship.Size.ToString(), ship.Direction.ToString());
			}
			
			return map;
		}

		private void PlaceShip(Map map, int size)
		{
			var cells = Vector.Rect(0, 0, width, height).OrderBy(v => random.Next());
			foreach (var loc in cells)
			{
				var horizontal = random.Next(2) == 0;
				if (map.Set(loc, size, horizontal) || map.Set(loc, size, !horizontal)) return;
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