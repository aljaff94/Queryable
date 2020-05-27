namespace Queryable.Models
{
    internal class QueryableResult
    {
        public QueryableResult(int count, object results)
        {
            Count = count;
            Results = results;
        }

        public int Count { get; set; }
        public object Results { get; set; }
    }
}