// Автор: Павел Егоров
// Дата: 28.12.2015

using System;
using System.Collections.Generic;
using System.Linq;

namespace battleships
{
	///<summary>Состояние клетки поля</summary>
	public enum MapCell
	{
		Empty = 0,

		Ship,

		DeadOrWoundedShip,
	
		Miss
	}

	///<summary>Результат выстрела</summary>
	public enum ShtEffct
	{
		
		Miss,
		
		Wound,
		
		Kill,
	}

	///<summary>Корабль</summary>
	public class Ship
	{
		///<summary>Конструктор</summary>
		public Ship(Vector location, int size, bool direction)
		{
			Location = location;
			Size = size;
			Direction = direction;
			AliveCells = new HashSet<Vector>(GetShipCells());
		}




		///<summary>Жив ли корабль</summary>
		public bool Alive { get { return AliveCells.Any(); } }
		///<summary>Позиция корабля на карте</summary>
		public Vector Location { get; private set; }


		
		
		
		///<summary>Клетки корабля</summary>
		public List<Vector> GetShipCells()
		{
			var d = Direction ? new Vector(1, 0) : new Vector(0, 1);
			var list1 = new List<Vector>();
			for (int i = 0; i < Size; i++)
			{
				var shipCell = d.Mult(i).Add(Location);
				list1.Add(shipCell);
			}
			return list1;
		}


		///<summary>Размер корабля</summary>
		public int Size { get; private set; }
		///<summary>Направление корабля. True — горизонтальное. False — вертикальное</summary>
		public bool Direction { get; private set; }
		///<summary>Живые клетки корабля</summary>
		public HashSet<Vector> AliveCells;
	}


	/////////////////////////////////////////////////////////////////////////////////////////////////
	/// Карта
	/////////////////////////////////////////////////////////////////////////////////////////////////


	///<summary>Карта</summary>
	public class Map
	{
		private static MapCell[,] cells;
		public static Ship[,] shipsMap;

		///<summary>Конструктор</summary>
		public Map(int width, int height)
		{
			Width = width;
			Height = height;
			cells = new MapCell[width, height];
			shipsMap = new Ship[width, height];
		}

		///<summary>Корабли на поле</summary>
		public List<Ship> Ships = new List<Ship>();

		///<summary>Ширина поля</summary>
		public int Width { get; private set; }
		///<summary>Высота поля</summary>
		public int Height { get; private set; }

		public MapCell this[Vector p]
		{
			get
			{
				return CheckBounds(p) ? cells[p.X, p.Y] : MapCell.Empty; // Благодаря этому трюку иногда можно будет не проверять на выход за пределы поля. 
			}
			private set
			{
				if (!CheckBounds(p))
					throw new IndexOutOfRangeException(p + " is not in the map borders"); // Поможет отлавливать ошибки в коде.
				cells[p.X, p.Y] = value;
			}
		}

		///<summary>Помещает корабль длинной i в точку v, смотрящий в направлении d</summary>
		public bool Set(Vector v, int n, bool direction)
		{
			var ship = new Ship(v, n, direction);
			var shipCells = ship.GetShipCells();
			//Если рядом есть непустая клетка, то поместить корабль нельзя!
			if (shipCells.SelectMany(Near).Any(c => this[c] != MapCell.Empty)) return false;
			//Если корабль не помещается — тоже нельзя
			if (!shipCells.All(CheckBounds)) return false;

			// Иначе, ставим корабль
			foreach (var cell in shipCells)
				{
					this[cell] = MapCell.Ship;
					shipsMap[cell.X, cell.Y] = ship;
				}
			Ships.Add(ship);
			return true;
		}

		///<summary>Бойтесь все!!!</summary>
		public ShtEffct Badaboom(Vector target)
		{
			var hit = CheckBounds(target) && this[target] == MapCell.Ship;
			
			
			if (hit)
			{
				var ship = shipsMap[target.X, target.Y];
				ship.AliveCells.Remove(target);
				this[target] = MapCell.DeadOrWoundedShip;
				return ship.Alive ? ShtEffct.Wound : ShtEffct.Kill;
			}


			if (this[target] == MapCell.Empty) this[target] = MapCell.Miss;
			return ShtEffct.Miss;
		}

		///<summary>Окрестность ячейки</summary>
		public IEnumerable<Vector> Near(Vector cell)
		{
			return
				from i in new[] {-1, 0, 1} //x
				from j in new[] {-1, 0, 1} //y
				let c = cell.Add(new Vector(i, j))
				where CheckBounds(c)
				select c;
		}

		///<summary>Проверка на выход за границы</summary>
		public bool CheckBounds(Vector p)
		{
			return p.X >= 0 && p.X < Width && p.Y >= 0 && p.Y < Height;
		}
		
		///<summary>Есть ли хоть одна живая клетка</summary>
		public bool HasAliveShips()
		{
			for (int index = 0; index < Ships.Count; index++)
			{
				var s = Ships[index];
				if (s.Alive) return true;
			}
			return false;
		}
	}
}