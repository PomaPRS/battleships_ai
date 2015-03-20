using System;
using System.Collections.Generic;
using System.Linq;

namespace SampleAi
{
	enum Direction
	{
		Horizontal,
		Vertical
	}

	enum CellStatus
	{
		Hidden,
		Wounded,
		Killed,
		Missed
	}

	enum ShotResult
	{
		Miss,
		Wound,
		Kill
	}

	enum ResponseType
	{
		Init,
		ShotResult
	}

	class Program
	{
		static void Main()
		{
			try
			{
				var messenger = new Messenger();
				var response = messenger.GetResponse();
				if (response == null || response.Type != ResponseType.Init)
					return;

				var game = new Game(response.GameInfo);
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
			return new Vector(k * X, k * Y);
		}
	}

	class Ship
	{
		public Ship(Vector location, int size, Direction direction)
		{
			Location = location;
			Size = size;
			Direction = direction;
		}

		public Vector Location { get; private set; }
		public int Size { get; private set; }
		public Direction Direction { get; private set; }

		public List<Vector> GetShipCells()
		{
			var direction = Direction == Direction.Horizontal ? new Vector(1, 0) : new Vector(0, 1);
			var shipCells = new List<Vector>();
			for (int i = 0; i < Size; i++)
			{
				var shipCell = direction.Mult(i).Add(Location);
				shipCells.Add(shipCell);
			}
			return shipCells;
		}
	}

	class Map
	{
		private readonly CellStatus[,] cells;

		public Map(int width, int height)
		{
			Widht = width;
			Height = height;
			cells = new CellStatus[width, height];
		}

		public int Widht { get; private set; }
		public int Height { get; private set; }

		public CellStatus this[Vector cell]
		{
			get { return CheckBounds(cell) ? cells[cell.X, cell.Y] : CellStatus.Hidden; }
			set
			{
				if (!CheckBounds(cell))
					throw new IndexOutOfRangeException(cell + " is not in the map borders");
				cells[cell.X, cell.Y] = value;
			}
		}

		public bool CanBeShip(Ship ship)
		{
			var shipCells = ship.GetShipCells();
			bool shipWithinMap = shipCells.All(CheckBounds);
			bool shipCellsAreNotMiss = shipCells.All(c => this[c] != CellStatus.Missed);
			if (!shipWithinMap || !shipCellsAreNotMiss)
				return false;

			var isShipCell = new bool[Widht, Height];
			foreach (var cell in shipCells)
				isShipCell[cell.X, cell.Y] = true;

			bool nearCellsAreHiddenOrMissed = shipCells
				.SelectMany(GetNearCells)
				.Where(c => !isShipCell[c.X, c.Y])
				.All(c => this[c] == CellStatus.Hidden || this[c] == CellStatus.Missed);
			return nearCellsAreHiddenOrMissed;
		}

		public IEnumerable<Vector> GetNearCells(IEnumerable<Vector> cells)
		{
			return cells.SelectMany(GetNearCells);
		}

		private IEnumerable<Vector> GetNearCells(Vector cell)
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
				from x in Enumerable.Range(0, Widht)
				from y in Enumerable.Range(0, Height)
				let cell = new Vector(x, y)
				where predicate(cell, this[cell])
				select cell;
		}

		public bool CheckBounds(Vector cell)
		{
			return cell.X >= 0 && cell.X < Widht && cell.Y >= 0 && cell.Y < Height;
		}
	}

	class Game
	{
		enum GameMode
		{
			SimpleAi,
			FindShip,
			KillShip
		}

		private int shipCellsCount;
		private Response lastResponse;
		private readonly int[] shipSizesCount;
		private readonly List<int> aliveShipSizes;
		private readonly Messenger messenger;
		private GameMode gameMode;

		public Game(GameInfo gameInfo)
		{
			messenger = new Messenger();
			Map = new Map(gameInfo.MapWidth, gameInfo.MapHeight);

			var maxShipSize = gameInfo.ShipSizes.Max();
			shipSizesCount = new int[maxShipSize + 1];
			foreach (var shipSize in gameInfo.ShipSizes)
			{
				shipSizesCount[shipSize]++;
			}

			shipCellsCount = gameInfo.ShipSizes.Sum();
			aliveShipSizes = gameInfo.ShipSizes
				.Where(size => size > 1)
				.Distinct()
				.ToList();

			gameMode = GameMode.FindShip;
			if (!aliveShipSizes.Any())
				gameMode = GameMode.SimpleAi;
		}

		public Map Map { get; private set; }

		public bool IsOver()
		{
			return shipCellsCount == 0;
		}

		public Game RunAndGetNextGame()
		{
			Run();
			if (lastResponse.Type != ResponseType.Init)
				return null;
			return new Game(lastResponse.GameInfo);
		}

		public void Run()
		{
			while (!IsOver())
			{
				MakeStep();
			}
		}

		public void MakeStep()
		{
			if (IsOver())
				throw new InvalidOperationException("Game is over");

			var target = GetTarget();
			MakeHit(target);
			UpdateGameStatus();
		}

		private Vector GetTarget()
		{
			Vector target = null;
			switch (gameMode)
			{
				case GameMode.SimpleAi:
					target = GetRandomTarget();
					break;
				case GameMode.FindShip:
					target = GetExpectedTarget();
					break;
				case GameMode.KillShip:
					target = GetTargetNearWoundedShips();
					break;
			}
			return target;
		}

		private void MakeHit(Vector target)
		{
			lastResponse = messenger.SendAndGetResponse(target);
		}

		private void UpdateGameStatus()
		{
			var lastShotInfo = lastResponse.ShotInfo;
			var lastShotResult = lastShotInfo.ShotResult;
			var target = lastShotInfo.Target;

			if (lastShotResult == ShotResult.Miss)
			{
				Map[target] = CellStatus.Missed;
			}
			else if (lastShotResult == ShotResult.Wound)
			{
				shipCellsCount--;
				gameMode = GameMode.KillShip;
				Map[target] = CellStatus.Wounded;
				MarkDiagonalCellsAsMissed(target);
			}
			else if (lastShotResult == ShotResult.Kill)
			{
				shipCellsCount--;
				gameMode = GameMode.FindShip;
				MarkLastKilledShip();
			}

			if (!aliveShipSizes.Any())
				gameMode = GameMode.SimpleAi;
		}

		private void MarkDiagonalCellsAsMissed(Vector target)
		{
			var diagonalCells = Map.GetDiagonalCells(target);
			foreach (var cell in diagonalCells)
			{
				Map[cell] = CellStatus.Missed;
			}
		}

		private void MarkLastKilledShip()
		{
			var shipCells = GetLastKilledShipCells().ToList();
			var nearShipCells = Map.GetNearCells(shipCells);

			var shipSize = shipCells.Count;
			shipSizesCount[shipSize]--;
			if (shipSizesCount[shipSize] == 0)
				aliveShipSizes.Remove(shipSize);

			foreach (var cell in nearShipCells)
			{
				if (Map[cell] == CellStatus.Hidden)
					Map[cell] = CellStatus.Missed;
				if (Map[cell] == CellStatus.Wounded)
					Map[cell] = CellStatus.Killed;
			}
		}

		private Vector GetRandomTarget()
		{
			var cells = Map.GetCells((c, s) => s == CellStatus.Hidden).ToList();
			var rand = new Random();
			return cells[rand.Next(cells.Count)];
		}

		private Vector GetExpectedTarget()
		{
			var cells = Map.GetCells((c, s) => s != CellStatus.Missed).ToList();
			var ships = GetPotentialShips(cells);
			var shipExpectation = GetShipExpectation(ships);
			return GetTarget(shipExpectation);
		}

		private Vector GetTargetNearWoundedShips()
		{
			var woundedShipCells = Map.GetCells((cell, status) => status == CellStatus.Wounded).ToList();
			var ships = GetPotentialShips(woundedShipCells);
			var shipExpectation = GetShipExpectation(ships);
			return GetTarget(shipExpectation);
		}

		/// <summary> Получает все возможные корабли, проходящие через заданные клетки </summary>
		private IEnumerable<Ship> GetPotentialShips(IEnumerable<Vector> cells)
		{
			var activeCells = new bool[Map.Widht, Map.Height];
			foreach (var cell in cells)
				activeCells[cell.X, cell.Y] = true;

			var horizontalShips = GetPotentialShips(Direction.Horizontal);
			var verticalShips = GetPotentialShips(Direction.Vertical);
			var ships = horizontalShips.Concat(verticalShips);
			return ships.Where(s => s.GetShipCells().Any(c => activeCells[c.X, c.Y]));
		}

		private IEnumerable<Ship> GetPotentialShips(Direction shipDirection)
		{
			return
				from x in Enumerable.Range(0, Map.Widht)
				from y in Enumerable.Range(0, Map.Height)
				from size in aliveShipSizes
				let location = new Vector(x, y)
				let ship = new Ship(location, size, shipDirection)
				where Map.CanBeShip(ship)
				select ship;
		}

		private int[,] GetShipExpectation(IEnumerable<Ship> ships)
		{
			var shipExpectation = new int[Map.Widht, Map.Height];
			foreach (var ship in ships)
			{
				var shipCells = ship.GetShipCells();
				foreach (var cell in shipCells)
				{
					shipExpectation[cell.X, cell.Y] += ship.Size * shipSizesCount[ship.Size];
				}
			}
			return shipExpectation;
		}

		private Vector GetTarget(int[,] shipExpectation)
		{
			var hiddenCells = Map
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

		private IEnumerable<Vector> GetLastKilledShipCells()
		{
			var lastShotInfo = lastResponse.ShotInfo;
			var target = lastShotInfo.Target;
			var shipCells = new List<Vector> { target };
			var directions = new[] { new Vector(1, 0), new Vector(-1, 0), new Vector(0, 1), new Vector(0, -1) };

			foreach (var direction in directions)
			{
				int size = 0;
				bool isKilledOrWoundedShipCell;
				do
				{
					size++;
					var cell = direction.Mult(size).Add(target);
					isKilledOrWoundedShipCell = Map[cell] == CellStatus.Killed || Map[cell] == CellStatus.Wounded;
					if (isKilledOrWoundedShipCell)
						shipCells.Add(cell);
				}
				while (isKilledOrWoundedShipCell);
			}
			return shipCells;
		}
	}

	class Messenger
	{
		public Response SendAndGetResponse(Vector target)
		{
			Send(target);
			var response = GetResponse();
			if (response == null)
			{
				var shotInfo = new ShotInfo(ShotResult.Kill, target);
				response = new Response(shotInfo);
			}
			else if (response.Type == ResponseType.Init)
			{
				var shotInfo = new ShotInfo(ShotResult.Kill, target);
				response = new Response(response.GameInfo, shotInfo);
			}
			return response;
		}

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
				var values = response.Split(' ');
				return Response.Parse(values);
			}
			catch (Exception e)
			{
				throw new InvalidOperationException("Error: bad response", e);
			}
		}
	}

	class Response
	{
		public Response(ShotInfo shotInfo)
		{
			Type = ResponseType.ShotResult;
			ShotInfo = shotInfo;
		}

		public Response(GameInfo gameInfo)
		{
			Type = ResponseType.Init;
			GameInfo = gameInfo;
		}

		public Response(GameInfo gameInfo, ShotInfo shotInfo)
			: this(gameInfo)
		{
			ShotInfo = shotInfo;
		}

		public ResponseType Type { get; private set; }
		public ShotInfo ShotInfo { get; private set; }
		public GameInfo GameInfo { get; private set; }

		public static Response Parse(params string[] values)
		{
			try
			{
				return values[0] == "Init" ? ParseInit(values) : ParseShotResult(values);
			}
			catch (Exception e)
			{
				throw new InvalidOperationException("Parsing error. See the inner exception for details", e);
			}
		}

		private static Response ParseInit(params string[] values)
		{
			var width = int.Parse(values[1]);
			var height = int.Parse(values[2]);
			var shipSizes = values.Skip(3).Select(int.Parse).ToArray();
			var gameInfo = new GameInfo(width, height, shipSizes);
			return new Response(gameInfo);
		}

		private static Response ParseShotResult(params string[] values)
		{
			var shotResult = (ShotResult)Enum.Parse(typeof(ShotResult), values[0]);
			var x = int.Parse(values[1]);
			var y = int.Parse(values[2]);
			var target = new Vector(x, y);
			var shotInfo = new ShotInfo(shotResult, target);
			return new Response(shotInfo);
		}
	}

	class ShotInfo
	{
		public ShotResult ShotResult { get; private set; }
		public Vector Target { get; private set; }

		public ShotInfo(ShotResult shotResult, Vector target)
		{
			ShotResult = shotResult;
			Target = target;
		}
	}

	class GameInfo
	{
		public GameInfo(int mapWidth, int mapHeight, params int[] shipSizes)
		{
			MapWidth = mapWidth;
			MapHeight = mapHeight;
			ShipSizes = shipSizes;
		}

		public int MapWidth { get; private set; }
		public int MapHeight { get; private set; }
		public int[] ShipSizes { get; private set; }
	}
}
