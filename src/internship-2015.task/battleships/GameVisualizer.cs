using System;
using System.Text;

namespace battleships
{
	public class GameVisualizer
	{
		public void Visualize(Game game)
		{
			Console.Clear();
			Console.WriteLine(MapToString(game));
			Console.WriteLine("Turn: {0}", game.TurnsCount);
			Console.WriteLine("Last target: {0}", game.LastTarget);
			if (game.BadShots > 0)
				Console.WriteLine("Bad shots: " + game.BadShots);
			if (game.IsOver())
				Console.WriteLine("Game is over");
		}

		private string MapToString(Game game)
		{
			var map = game.Map;
			var sb = new StringBuilder();
			for (var y = 0; y < map.Height; y++)
			{
				for (var x = 0; x < map.Width; x++)
					sb.Append(GetSymbol(map[new Vector(x, y)]));
				sb.AppendLine();
			}
			return sb.ToString();
		}

		private string GetSymbol(MapCell cell)
		{
			switch (cell)
			{
				case MapCell.Empty:
					return " ";
				case MapCell.Miss:
					return "*";
				case MapCell.Ship:
					return "O";
				case MapCell.DeadOrWoundedShip:
					return "X";
				default:
					throw new Exception(cell.ToString());
			}
		}
	}
}