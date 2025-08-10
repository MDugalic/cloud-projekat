using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdminToolsConsoleApp
{
    public class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Admin Tools ===");
            Console.WriteLine("1) Add alert email");
            Console.WriteLine("2) Remove alert email");
            Console.WriteLine("3) Verify user as author");
            Console.Write("Choose: ");
            var c = Console.ReadLine();

            switch (c)
            {
                case "1":
                    Console.Write("Email: ");
                    new AlertEmailsTable().Add(Console.ReadLine().ToLowerInvariant());
                    Console.WriteLine("Added.");
                    break;

                case "2":
                    Console.Write("Email: ");
                    new AlertEmailsTable().Remove(Console.ReadLine().ToLowerInvariant());
                    Console.WriteLine("Removed (if existed).");
                    break;

                case "3":
                    Console.Write("User email: ");
                    var ok = new AuthorVerifier().Verify(Console.ReadLine());
                    Console.WriteLine(ok ? "Verified." : "User not found.");
                    break;

                default:
                    Console.WriteLine("Unknown option.");
                    break;
            }
        }
    }
}
