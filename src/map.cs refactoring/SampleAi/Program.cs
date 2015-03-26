using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SampleAi
{
	class Program
	{
		// Управляющая программа battleships.exe будет запускать этот файл и перенаправлять стандартные потоки ввода и вывода.
		//
		// Вам нужно читать информацию с консоли и писать команды на консоль.
		// Конец ввода — это сигнал к завершению программы.

		static void Main()
		{
			var r = new Random();
			while (true)
			{
				var line = Console.ReadLine();
				if (line == null) return;
				// line имеет один из следующих форматов:
				// Init <map_width> <map_height> <ship1_size> <ship2_size> ...
				// Wound <last_shot_X> <last_shot_Y>
				// Kill <last_shot_X> <last_shot_Y>
				// Miss <last_shot_X> <last_shot_Y>
				// Один экземпляр вашей программы может быть использван для проведения нескольких игр подряд.
				// Сообщение Init сигнализирует о том, что началась новая игра.
				Console.WriteLine("{0} {1}", r.Next(20), r.Next(20));
			}
		}
	}
}
