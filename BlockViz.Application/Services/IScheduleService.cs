using System.Collections.Generic;
using BlockViz.Domain.Models;

namespace BlockViz.Applications.Services
{
    public interface IScheduleService
    {
        void SetAllBlocks(IEnumerable<Block> blocks);

        IEnumerable<Block> GetAllBlocks();
    }
}