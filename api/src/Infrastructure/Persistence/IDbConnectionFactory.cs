using System.Data.Common;

namespace BallastLane.Infrastructure.Persistence;

public interface IDbConnectionFactory
{
    DbConnection Create();
}
