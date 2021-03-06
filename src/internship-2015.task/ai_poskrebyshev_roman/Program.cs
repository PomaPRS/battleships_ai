﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace ai_poskrebyshev_roman
{
	enum ShotResult
	{
		Miss,
		Wound,
		Kill
	}

	enum CellStatus
	{
		Hidden,
		Wounded,
		Killed,
		Missed
	}

	enum Direction
	{
		Horizontal,
		Vertical
	}

	class Program
	{
		static void Main(string[] args)
		{
			try
			{
				var messager = new Messenger();
				var response = messager.GetResponse() as GameInfo;
				if (response == null) return;

				var game = new Game(response, messager);
				while (game != null)
				{
					game = game.RunAndGetNextGame();
				}
			}
			catch (Exception e)
			{
				throw new Exception("Strategy crashed. See the inner exception for details.", e);
			}
		}
	}

	class Game
	{
		private readonly Map map;
		private readonly Random random;
		private readonly Messenger messenger;
		private readonly List<int> aliveShipSizes; 
		private int shipCellsCount;
		private Game nextGame;

		public Game(GameInfo gameInfo, Messenger messenger)
		{
			this.messenger = messenger;
			random = new Random();
			map = new Map(gameInfo.Width, gameInfo.Height);

			shipCellsCount = gameInfo.ShipSizes.Sum();
			aliveShipSizes = gameInfo.ShipSizes.ToList();
		}

		public bool Over
		{
			get { return shipCellsCount == 0; }
		}

		public Game RunAndGetNextGame()
		{
			Run();
			return nextGame;
		}

		public void Run()
		{
			while (!Over)
			{
				MakeStep();
			}
		}

		public void Finish()
		{
			shipCellsCount = 0;
		}
		
		public void MakeStep()
		{
			if (Over)
				throw new InvalidOperationException("Game is over");

			Strategy strategy;
			if (aliveShipSizes.Any(x => x > 1))
			{
				strategy = new SmartStrategy(map, aliveShipSizes.ToArray());
			}
			else
			{
				strategy = new RandomStrategy(map, random);
			}

			var response = MakeHit(strategy.GetTarget());
			UpdateGameStatus(response);
		}

		private Response MakeHit(Vector target)
		{
			messenger.Send(target);
			return messenger.GetResponse();
		}

		private void UpdateGameStatus(Response response)
		{
			var shot = response as ShotInfo;
			if (shot == null)
			{
				var gameInfo = response as GameInfo;
				if (gameInfo != null)
					nextGame = new Game(gameInfo, messenger);
				Finish();
				return;
			}
			
			switch (shot.Result)
			{
				case ShotResult.Miss:
					ProcessMiss(shot.Target);
					break;
				case ShotResult.Wound:
					ProcessWound(shot.Target);
					break;
				case ShotResult.Kill:
					ProcessKill(shot.Target);
					break;
			}
		}

		private void ProcessMiss(Vector target)
		{
			map[target] = CellStatus.Missed;
		}

		private void ProcessWound(Vector target)
		{
			shipCellsCount--;
			map[target] = CellStatus.Wounded;
			var diagonalCells = map.GetDiagonalCells(target);
			foreach (var cell in diagonalCells)
			{
				map[cell] = CellStatus.Missed;
			}
		}

		private void ProcessKill(Vector target)
		{
			shipCellsCount--;
			map[target] = CellStatus.Wounded;
			var shipCells = GetWoundedShipCells(target).ToList();
			var shipSize = shipCells.Count;
			aliveShipSizes.Remove(shipSize);

			var nearShipCells = shipCells.SelectMany(c => map.GetNearCells(c));
			foreach (var cell in nearShipCells)
			{
				if (map[cell] == CellStatus.Hidden)
					map[cell] = CellStatus.Missed;
				if (map[cell] == CellStatus.Wounded)
					map[cell] = CellStatus.Killed;
			}
		}

		private IEnumerable<Vector> GetWoundedShipCells(Vector target)
		{
			if (map[target] != CellStatus.Wounded) 
				return Enumerable.Empty<Vector>();

			var directions = new[] { new Vector(1, 0), new Vector(-1, 0), new Vector(0, 1), new Vector(0, -1) };
			return directions.SelectMany(x => GetWoundedShipCells(target.Add(x), x)).Concat(new[] {target});
		}

		private IEnumerable<Vector> GetWoundedShipCells(Vector target, Vector shift)
		{
			var maxShift = Math.Max(map.Width, map.Height);
			return Enumerable.Range(0, maxShift)
				.Select(x => shift.Mult(x).Add(target))
				.TakeWhile(cell => map[cell] == CellStatus.Wounded);
		}
	}

	class RandomStrategy : Strategy
	{
		private readonly Random random;

		public RandomStrategy(Map map, Random random) : base(map)
		{
			this.random = random;
		}

		public override Vector GetTarget()
		{
			var cells = map.GetCells((c, s) => s == CellStatus.Hidden).ToList();
			var cellId = random.Next(cells.Count);
			return cells[cellId];
		}
	}

	class SmartStrategy : Strategy
	{
		private readonly List<int> aliveShipSizes;
		private readonly Dictionary<int, int> shipSizesCount;

		public SmartStrategy(Map map, int[] aliveShipSizes) : base(map)
		{
			this.aliveShipSizes = aliveShipSizes.Where(x => x > 1).Distinct().ToList();
			shipSizesCount = aliveShipSizes.GroupBy(x => x).ToDictionary(x => x.Key, x => x.Count());
		}

		public override Vector GetTarget()
		{
			var cells = map.GetCells((c, s) => s == CellStatus.Hidden || s == CellStatus.Wounded).ToList();
			var ships = GetMaxWoundedShips(cells);
			var shipExpectation = GetShipExpectation(ships.ToList());
			return GetTarget(shipExpectation);
		}

		private Vector GetTarget(int[,] shipExpectation)
		{
			var hiddenCells = map
				.GetCells((c, s) => s == CellStatus.Hidden)
				.Select(c => new { Cell = c, Expectation = shipExpectation[c.X, c.Y] })
				.ToList();

			var maxExpectation = hiddenCells.Any() ? hiddenCells.Max(c => c.Expectation) : 0;
			if (maxExpectation == 0)
				return null;

			var targets = hiddenCells.Where(c => c.Expectation == maxExpectation).ToList();
			var targetCount = targets.Count();
			var random = new Random();
			var targetId = random.Next(targetCount);
			return targets[targetId].Cell;
		}

		private IEnumerable<Ship> GetMaxWoundedShips(List<Vector> location)
		{
			var ships = GetPotentialShips(location).ToList();
			var woundedCellsCounts = ships.Select(s =>
			{
				var shipCells = s.GetShipCells().ToList();
				var woundedCellsCount = shipCells.Count(x => map[x] == CellStatus.Wounded);
				return new { Ship = s, WoundedCellsCount = woundedCellsCount };
			}).ToList();

			var woundedCellsMax = woundedCellsCounts.Max(x => x.WoundedCellsCount);
			return woundedCellsCounts.Where(x => x.WoundedCellsCount == woundedCellsMax).Select(x => x.Ship).ToList();
		}

		private IEnumerable<Ship> GetPotentialShips(List<Vector> locations)
		{
			var horizontalShips = GetPotentialShips(locations, Direction.Horizontal);
			var verticalShips = GetPotentialShips(locations, Direction.Vertical);
			return horizontalShips.Concat(verticalShips);
		}

		private IEnumerable<Ship> GetPotentialShips(IEnumerable<Vector> locations, Direction direction)
		{
			return
				from location in locations
				from shipSize in aliveShipSizes
				let ship = new Ship(location, shipSize, direction)
				where map.CanBeShip(ship)
				select ship;
		}

		private int[,] GetShipExpectation(IEnumerable<Ship> ships)
		{
			var shipExpectation = new int[map.Width, map.Height];
			foreach (var ship in ships)
			{
				var shipCells = ship.GetShipCells().ToList();

				foreach (var cell in shipCells)
				{
					shipExpectation[cell.X, cell.Y] += ship.Size * shipSizesCount[ship.Size];
				}
			}
			return shipExpectation;
		}
	}

	abstract class Strategy
	{
		protected Map map;

		public Strategy(Map map)
		{
			this.map = map;
		}

		public abstract Vector GetTarget();
	}

	class Ship
	{
		public Ship(Vector location, int size, Direction direction)
		{
			Direction = direction;
			Size = size;
			Location = location;
		}

		public Vector Location { get; private set; }
		public int Size { get; private set; }
		public Direction Direction { get; private set; }

		public IEnumerable<Vector> GetShipCells()
		{
			var direction = Direction == Direction.Horizontal ? new Vector(1, 0) : new Vector(0, 1);
			return Enumerable.Range(0, Size).Select(x => direction.Mult(x).Add(Location));
		}
	}

	class Map
	{
		private CellStatus[,] cells;

		public Map(int width, int height)
		{
			Height = height;
			Width = width;
			cells = new CellStatus[width, height];
		}

		public int Width { get; private set; }
		public int Height { get; private set; }

		public CellStatus this[Vector cell]
		{
			get { return CheckBounds(cell) ? cells[cell.X, cell.Y] : CellStatus.Missed; }
			set
			{
				if (!CheckBounds(cell))
					throw new IndexOutOfRangeException(cell + " is not in the map borders");
				cells[cell.X, cell.Y] = value;
			}
		}

		public bool CanBeShip(Ship ship)
		{
			var shipCells = ship.GetShipCells().ToList();
			bool shipWithinMap = shipCells.All(CheckBounds);
			bool shipCellsAreNotMiss = shipCells.All(c => this[c] != CellStatus.Missed);
			if (!shipWithinMap || !shipCellsAreNotMiss)
				return false;

			var isShipCell = new bool[Width, Height];
			foreach (var cell in shipCells)
				isShipCell[cell.X, cell.Y] = true;

			bool nearCellsAreHiddenOrMissed = shipCells
				.SelectMany(GetNearCells)
				.Where(c => !isShipCell[c.X, c.Y])
				.All(c => this[c] == CellStatus.Hidden || this[c] == CellStatus.Missed);
			return nearCellsAreHiddenOrMissed;
		}

		public IEnumerable<Vector> GetNearCells(Vector cell)
		{
			return
				from x in new[] { -1, 0, 1 }
				from y in new[] { -1, 0, 1 }
				let c = new Vector(x, y).Add(cell)
				where CheckBounds(c)
				select c;
		}

		public IEnumerable<Vector> GetDiagonalCells(Vector cell)
		{
			var diagonals = new[] { new Vector(1, 1), new Vector(1, -1), new Vector(-1, 1), new Vector(-1, -1) };
			return diagonals
				.Select(cell.Add)
				.Where(CheckBounds);
		}

		public IEnumerable<Vector> GetCells(Func<Vector, CellStatus, bool> predicate)
		{
			return
				from x in Enumerable.Range(0, Width)
				from y in Enumerable.Range(0, Height)
				let cell = new Vector(x, y)
				where predicate(cell, this[cell])
				select cell;
		}

		public bool CheckBounds(Vector cell)
		{
			return cell.X >= 0 && cell.X < Width && cell.Y >= 0 && cell.Y < Height;
		}
	}

	class Messenger
	{
		public void Send(Vector target)
		{
			Console.WriteLine("{0} {1}", target.X, target.Y);
		}

		public Response GetResponse()
		{
			try
			{
				var response = Console.ReadLine();
				if (response == null)
					return null;
				var parser = new ResponseParser(response);
				return parser.GetResponse();
			}
			catch (Exception e)
			{
				throw new InvalidOperationException("Bad response. See the inner exception for details", e);
			}
		}
	}

	class ResponseParser
	{
		public ResponseParser(string message)
		{
			Message = message;
		}

		public string Message { get; private set; }

		public Response GetResponse()
		{
			try
			{
				var values = Message.Split(' ');
				return values[0] == "Init" ? (Response)ParseInit(values) : ParseShotResult(values);
			}
			catch (Exception e)
			{
				throw new InvalidOperationException("Parsing error. See the inner exception for details", e);
			}
		}

		private static GameInfo ParseInit(params string[] values)
		{
			var width = int.Parse(values[1]);
			var height = int.Parse(values[2]);
			var shipSizes = values.Skip(3).Select(int.Parse).ToArray();
			return new GameInfo(width, height, shipSizes);
		}

		private static ShotInfo ParseShotResult(params string[] values)
		{
			var shotResult = (ShotResult)Enum.Parse(typeof(ShotResult), values[0]);
			var x = int.Parse(values[1]);
			var y = int.Parse(values[2]);
			var target = new Vector(x, y);
			return new ShotInfo(shotResult, target);
		}
	}

	class ShotInfo : Response
	{
		public ShotInfo(ShotResult result, Vector target)
		{
			Result = result;
			Target = target;
		}

		public ShotResult Result { get; private set; }
		public Vector Target { get; private set; }
	}

	class GameInfo : Response
	{
		public GameInfo(int width, int height, params int[] shipSizes)
		{
			Width = width;
			Height = height;
			ShipSizes = shipSizes;
		}

		public int Width { get; private set; }
		public int Height { get; private set; }
		public int[] ShipSizes { get; private set; }
	}

	abstract class Response
	{
	}

	class Vector
	{
		public Vector(int x, int y)
		{
			X = x;
			Y = y;
		}

		public int X { get; private set; }
		public int Y { get; private set; }

		public Vector Add(Vector other)
		{
			return new Vector(X + other.X, Y + other.Y);
		}

		public Vector Mult(int k)
		{
			return new Vector(k*X, k*Y);
		}
	}
}