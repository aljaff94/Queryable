using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Queryable.Models;

namespace Queryable.Filters
{
    public class QueryableAttribute : Attribute, IActionFilter
    {

        public string NormalizeFilter(string equation)
        {
            switch (equation)
            {
                case "eq": return "x.{0} == {1}";
                case "nq": return "x.{0} != {1}";
                case "lk": return "x.{0}.Contains({1})";
                case "nk": return "!x.{0}.Contains({1})";
                case "ge": return "x.{0} >= {1}";
                case "gt": return "x.{0} > {1}";
                case "le": return "x.{0} <= {1}";
                case "lt": return "x.{0} < {1}";
                default: return "";
            }
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            var results = (context.Result as ObjectResult).Value as IEnumerable<object>;
            var resultsCount = results?.Count();
            var count = true;

            if (context.HttpContext.Request.Query.TryGetValue("$count", out var _count))
            {
                if (_count.ToString().Trim().ToLower() == "false")
                {
                    count = false;
                }
            }

            try
            {

                if (results == null || resultsCount <= 0)
                {
                    context.Result = count ? new ObjectResult(new QueryableResult(0, null)) : new ObjectResult(context.Result);
                }

                if (context.HttpContext.Request.Query.TryGetValue("$filter", out var _filter))
                {
                    if (!string.IsNullOrWhiteSpace(_filter))
                    {
                        StringBuilder linqBuilder = new StringBuilder("x => ");
                        var andParts = _filter.ToString().ToLower().Split(" and ");
                        for (int i = 0; i < andParts.Length; i++)
                        {
                            if (i > 0)
                            {
                                linqBuilder.Append(" && ");
                            }
                            var orParts = andParts[i].Split(" or ");
                            for (int j = 0; j < orParts.Length; j++)
                            {
                                if (j > 0)
                                {
                                    linqBuilder.Append(" || ");
                                }
                                var parts = Regex.Matches(orParts[j].Trim(), @"(?<match>\w+)|\""(?<match>[\w\s]*)""").Cast<Match>().Select(m => m.Groups["match"].Value).ToList();
                                linqBuilder.AppendFormat(NormalizeFilter(parts[1]), parts[0], double.TryParse(parts[2], out var _t) ? parts[2] : $"\"{parts[2]}\"");
                            }
                        }
                        var z = linqBuilder.ToString();
                        results = results.WhereDynamic(linqBuilder.ToString());
                    }
                }


                if (context.HttpContext.Request.Query.TryGetValue("$order", out var order))
                {
                    if (!string.IsNullOrWhiteSpace(order))
                    {
                        bool isFirstOrder = true;
                        foreach (var item in ((string)order).Split(","))
                        {
                            var parts = item.Trim().Split(' ');
                            if (isFirstOrder)
                            {
                                isFirstOrder = false;
                                switch (parts[0].Trim().ToLower())
                                {
                                    case "asc": results = results.OrderByDynamic($"x => x.{parts[1].Trim()}"); break;
                                    case "desc": results = results.OrderByDescendingDynamic($"x => x.{parts[1].Trim()}"); break;
                                }
                            }
                            else
                            {
                                switch (parts[0].Trim().ToLower())
                                {
                                    case "asc": results = (IEnumerable<object>)results.ThenByDynamic($"x => x.{parts[1].Trim()}"); break;
                                    case "desc": results = (IEnumerable<object>)results.ThenByDescendingDynamic($"x => x.{parts[1].Trim()}"); break;
                                }
                            }
                        }
                    }
                }

                if (context.HttpContext.Request.Query.TryGetValue("$select", out var select))
                {
                    if (!string.IsNullOrWhiteSpace(select))
                    {
                        StringBuilder sb = new StringBuilder();
                        foreach (var item in ((string)select).Split(','))
                        {
                            var parts = item.Trim().Split(' ');
                            if (parts.Count() == 2)
                            {
                                sb.Append($"{parts[0]} = x.{parts[1]},");
                            }
                            else
                            {
                                sb.Append($"x.{item.Trim()},");
                            }
                        }
                        results = results.SelectDynamic(x => $"new {{{sb.ToString().Substring(0, sb.Length - 1)}}}");
                    }
                }

                resultsCount = results?.Count();

                // skip pages
                int page = 1;
                if (context.HttpContext.Request.Query.TryGetValue("$page", out var pageAsStr))
                {
                    int.TryParse(pageAsStr, out page);
                }

                // take length
                int length = 10;
                if (context.HttpContext.Request.Query.TryGetValue("$length", out var lengthAsStr))
                {
                    int.TryParse(lengthAsStr, out length);
                }


                if (resultsCount > length)
                {
                    var totalSkip = (page - 1) * length;
                    if (totalSkip > 0 && resultsCount > totalSkip)
                    {
                        results = results.Skip(totalSkip);
                    }
                    if (resultsCount > length)
                    {
                        results = results.Take(length);
                    }
                }

                context.Result = count ? new ObjectResult(new QueryableResult(resultsCount.Value, results)) : new ObjectResult(results);
            }
            catch (System.Exception ex)
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(QueryableAttribute));
                logger.LogError(ex, "Error in Queryable equation");
                context.Result = count ? new ObjectResult(new QueryableResult(-1, null)) : new ObjectResult(null);
            }
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {

        }
    }
}