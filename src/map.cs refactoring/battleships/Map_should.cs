using NUnit.Framework;

namespace battleships
{
	[TestFixture]
	public class Map_should
	{
		[Test]
		public void put_ship_inside_map_bounds()
		{
			var map = new Map(100, 10);
			Assert.IsTrue(map.SetShip(new Vector(0, 0), 5, Direction.Horizontal));
			Assert.IsTrue(map.SetShip(new Vector(95, 9), 5, Direction.Horizontal));
		}

		[Test]
		public void not_put_ship_outside_map()
		{
			var map = new Map(100, 10);
			Assert.IsFalse(map.SetShip(new Vector(99, 9), 2, Direction.Horizontal));
			Assert.IsFalse(map.SetShip(new Vector(99, 9), 2, Direction.Vertical));
		}

		[Test]
		public void kill_ship()
		{
			var map = new Map(100, 10);
			map.SetShip(new Vector(0, 0), 1, Direction.Horizontal);
			Assert.AreEqual(ShotResult.Kill, map.MakeHit(new Vector(0, 0)));
			Assert.AreEqual(MapCell.DeadOrWoundedShip, map[new Vector(0, 0)]);
		}

		[Test]
		public void wound_ship()
		{
			var map = new Map(100, 10);
			map.SetShip(new Vector(0, 0), 2, Direction.Horizontal);
			Assert.AreEqual(ShotResult.Wound, map.MakeHit(new Vector(0, 0)));
			Assert.AreEqual(MapCell.DeadOrWoundedShip, map[new Vector(0, 0)]);
			Assert.AreEqual(MapCell.Ship, map[new Vector(1, 0)]);
		}
	}
}