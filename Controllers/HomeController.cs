//#define PCRE_DEBUG

using System;
using System.Linq;
using System.Web.Http;
using System.Xml.Linq;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using PCRE;

using SerkoTest2.Utils;

namespace SerkoTest2.Controllers
{
	public class ExpenseController : ApiController
	{
		private const string EXPENSE = "expense";

		private const string PATTERN = @"
			(?(DEFINE)		# Define, not execute:															# Define a subroutine to match xml fragments:
				(?<xml>			# NamedGroup <xml> is: 														#
					<				# '<'																	# When matching the start tag, and elsewhere,
					(?<tag>			# NamedGroup <tag> is:													#  we are posessive so that any backtracking
						\w++			# OneOrMore(posessive): any word character (typically [A-Za-z_0-9])	#  will skip the entire <tag> match, rather than
					)																						#  trying one char less each time, when we know that
					(				# Either:																#  won't do any better.
							/				# '/'															# A similar construct is the atomic group: (?>...),
						|				# Or:																#  but I didn't need to use that here.
							>				# '>'															#
							\s*+			# ZeroOrMore(posessive): whitespace								#
							(?<value>		# NamedGroup <value> is: OneOrMore(posessive): Either:			# I'm using PCRE rather than .Net Regex specifically for recursion.
									(?&xml)			# Recursively match the <xml> subroutine				#
								|				# Or:														#
									[^<>]++			# OneOrMore(posessive): Not '<' nor '>'					#
							)++																				#
							\s*+			# ZeroOrMore(posessive): whitespace								#
							</				# '</'															#
							\k<tag>			# The current match of <tag>									#
					)																						#
					>				# '>'																	#
					(?(<xml>)\s*+)	# If <xml> has been matched: ZeroOrMore(posessive): whitespace; 		# Don't match this at the top level match, only at deeper ones.
				)																							#
			)				# /Define.																		#
																											#
			(?&xml)			# Call the <xml> subroutine.													# Do the work.
		";

#if ANSI_TERMINAL
		private const string HILITE = "\x1b[1;31m{\x1b[33m";
		private const string NORMAL = "\x1b[31m}\x1b[0m;";
#else
		private const string HILITE = "";
		private const string NORMAL = "";
#endif


		private static readonly string[] RequiredKeys =
		{
			"total",
			"cost_centre"
		};

		private static readonly PcreRegex ExtractXmlRE = new PcreRegex(
			PATTERN,
			PcreOptions.ExplicitCapture | PcreOptions.IgnorePatternWhitespace | PcreOptions.AutoCallout
		);

		private static readonly PcreRegex LooseXmlRE = new PcreRegex(@"<\w+>");
		private static readonly PcreRegex DebugRE = new PcreRegex(@"(?:[\t\n\r]+|#.*)");


		[HttpPost]
		public object Post([FromBody] string text)
		{
			if (!ModelState.IsValid)
			{
				return ModelState;
			}


			string error;
			var result = ExtractXml(text, out error);
			return (error == null)
				? result
				: (object)error;
		}


		private static JObject ExtractXml(string text, out string error)
		{
			var result = new JObject
			{
				{ EXPENSE, new JObject() },
				{ "_original", text }
			};

			foreach (PcreMatch match in ExtractXmlRE.Matches(text
#if PCRE_DEBUG
				, 0, AutoCalloutHandler(text)
#endif
			))
			{
				ProcessXmlFragment(match.Value, result);
			}

			var expense = (JObject)result[EXPENSE];
			JToken _;
			var errors =
				from requiredKey in RequiredKeys
				where !expense.TryGetValue(requiredKey, out _)
				select $"Required key '{requiredKey}' was not found!";

			if (LooseXmlRE.IsMatch(ExtractXmlRE.Replace(text, "")))
			{
				errors = errors.Concat(new[] { "Malformed XML found!" });
			}


			error = string.Join(Environment.NewLine, errors).NullIfEmpty();
			return result;
		}

#if PCRE_DEBUG
		private static Func<PcreCallout, PcreCalloutResult> AutoCalloutHandler(string text)
		{
			return arg =>
			{
				var patternStart = Math.Max(0, arg.PatternPosition - 20);
				var patternEnd = Math.Min(PATTERN.Length - 1, arg.PatternPosition + arg.NextPatternItemLength + 20);
				var pattern = (
					PATTERN.Substring(patternStart, arg.PatternPosition - patternStart)
					+ HILITE + PATTERN.Substring(arg.PatternPosition, arg.NextPatternItemLength) + NORMAL
					+ PATTERN.Substring(
						arg.PatternPosition + arg.NextPatternItemLength,
						patternEnd - (arg.PatternPosition + arg.NextPatternItemLength - 1)
					)
				);

				var textStart = Math.Max(0, arg.Match.Index - 20);
				var textEnd = Math.Min(text.Length - 1, arg.Match.EndIndex + 20);
				var at = (
					text.Substring(textStart, arg.Match.Index - textStart)
					+ HILITE + arg.Match.Value + NORMAL
					+ text.Substring(arg.Match.EndIndex, textEnd - arg.Match.EndIndex)
				).Replace("\t", "\\t").Replace("\n", "\\n").Replace("\r", "\\r");

				Console.WriteLine(DebugRE.Replace(pattern, ""));
#if !ANSI_TERMINAL
				Console.WriteLine(
					new string(' ', DebugRE.Replace(PATTERN.Substring(patternStart, arg.PatternPosition - patternStart), "").Length)
					+ new string('^', DebugRE.Replace(PATTERN.Substring(arg.PatternPosition, arg.NextPatternItemLength), "").Length)
				);
#endif

				Console.WriteLine(at);
#if !ANSI_TERMINAL
				Console.WriteLine(
					new string(' ', text.Substring(textStart, arg.Match.Index - textStart).Replace("\t", "\\t").Replace("\n", "\\n").Replace("\r", "\\r").Length)
					+ new string('^', Math.Max(1, arg.Match.Length))
				);
#endif
				//Console.WriteLine($"mi:{arg.Match.Index},me:{arg.Match.EndIndex},ml:{arg.Match.Length},cc:{arg.Match.CaptureCount},{arg.Match.Success},{(arg.Match.Success ? $"'{arg.Match.Value}'" : "---")}");

				Console.WriteLine($"co:{arg.CurrentOffset},lc:{arg.LastCapture},xc:{arg.MaxCapture},sm:{arg.StartMatch},so:{arg.StartOffset},'{arg.String}',So:{arg.StringOffset},bt:{arg.Backtrack}");
				Console.WriteLine(string.Join("; ", arg.Match.Groups.Select(g => $"@{g.Index},{g.Success},{(g.Success ? $"'{g.Value}'" : "---")}")));

				Console.WriteLine();

				return PcreCalloutResult.Pass;
			};
		}
#endif

		private static void ProcessXmlFragment(string matchValue, JObject result)
		{
			var xElement = XElement.Parse(matchValue);
			var jElement = (JObject)JsonConvert.DeserializeObject(JsonConvert.SerializeObject(xElement));
			JToken _;
			if (jElement.TryGetValue(EXPENSE, out _))
			{
				result.Merge(jElement);
			}
			else
			{
				foreach (var pair in jElement)
				{
					((JObject)result[EXPENSE]).Add(pair.Key, pair.Value);
				}
			}
		}
	}
}
