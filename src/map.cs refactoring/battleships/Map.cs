using System;
using System.Collections.Generic;
using System.Linq;

namespace battleships
{
	public enum MapCell
	{
		Empty,
		Ship,
		DeadOrWoundedShip,
		Miss
	}

	public enum ShotResult
	{
		Miss,
		Wound,
		Kill
	}

	public enum Direction
	{
		Horizontal,
		Vertical
	}

	public class Ship
	{
		public Ship(Vector location, int size, Direction direction)
		{
			Location = location;
			Size = size;
			Direction = direction;
			AliveCells = new HashSet<Vector>(GetShipCells());
		}

		public Vector Location { get; private set; }
		public int Size { get; private set; }
		public Direction Direction { get; private set; }
		public HashSet<Vector> AliveCells { get; private set; }

		public bool Alive
		{
			get { return AliveCells.Any(); }
		}

		public List<Vector> GetShipCells()
		{
			var direction = Direction == Direction.Horizontal ? new Vector(1, 0) : new Vector(0, 1);
			return Enumerable.Range(0, Size)
				.Select(x => direction.Mult(x).Add(Location))
				.ToList();
		}
	}

	public class Map
	{
		private MapCell[,] cells;
		private Ship[,] shipsMap;

		public Map(int width, int height)
		{
			Width = width;
			Height = height;
			Ships = new List<Ship>();
			cells = new MapCell[width, height];
			shipsMap = new Ship[width, height];
		}

		public int Width { get; private set; }
		public int Height { get; private set; }
		public List<Ship> Ships { get; private set; }

		public MapCell this[Vector cell]
		{
			get
			{
				return CheckBounds(cell) ? cells[cell.X, cell.Y] : MapCell.Empty;
			}
			private set
			{
				if (!CheckBounds(cell))
					throw new IndexOutOfRangeException(cell + " is not in the map borders");
				cells[cell.X, cell.Y] = value;
			}
		}

		public bool SetShip(Vector location, int size, Direction direction)
		{
			var ship = new Ship(location, size, direction);
			var shipCells = ship.GetShipCells();
			var nearCells = shipCells.SelectMany(GetNearCells);

			var canPlace = shipCells.All(CheckBounds);
			var hasOnlyEmptyCells = nearCells.Any(c => this[c] == MapCell.Empty);
			if (!canPlace || !hasOnlyEmptyCells) 
				return false;

			foreach (var cell in shipCells)
			{
				this[cell] = MapCell.Ship;
				shipsMap[cell.X, cell.Y] = ship;
			}
			Ships.Add(ship);
			return true;
		}

		public ShotResult MakeHit(Vector target)
		{
			if (this[target] == MapCell.Ship)
			{
				var ship = shipsMap[target.X, target.Y];
				ship.AliveCells.Remove(target);
				this[target] = MapCell.DeadOrWoundedShip;
				return ship.Alive ? ShotResult.Wound : ShotResult.Kill;
			}

			if (this[target] == MapCell.Empty) 
				this[target] = MapCell.Miss;
			return ShotResult.Miss;
		}

		public IEnumerable<Vector> GetNearCells(Vector cell)
		{
			return
				from x in new[] {-1, 0, 1}
				from y in new[] {-1, 0, 1}
				let c = cell.Add(new Vector(x, y))
				where CheckBounds(c)
				select c;
		}

		public bool CheckBounds(Vector cell)
		{
			return cell.X >= 0 && cell.X < Width && cell.Y >= 0 && cell.Y < Height;
		}
		
		public bool HasAliveShips()
		{
			return Ships.Any(s => s.Alive);
		}

		public Ship GetShipAt(Vector cell)
		{
			return shipsMap[cell.X, cell.Y];
		}
	}
}