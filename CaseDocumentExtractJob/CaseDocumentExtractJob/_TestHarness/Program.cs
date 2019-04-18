using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace _TestHarness
{
  class Program
  {
    static void Main(string[] args)
    {
      JobTester test = new JobTester();

      try
      {
        test.Test();
      }
      catch (Exception e)
      {
        Console.WriteLine(e.ToString());
      }

      Console.ReadKey();
    }
  }
}
