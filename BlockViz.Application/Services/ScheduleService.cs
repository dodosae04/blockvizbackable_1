using System.Collections.Generic;
using System.ComponentModel.Composition;
using BlockViz.Domain.Models;

namespace BlockViz.Applications.Services
{
    
    [Export(typeof(IScheduleService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class ScheduleService : IScheduleService
    {
        private IEnumerable<Block> allBlocks = new List<Block>();

        
        public void SetAllBlocks(IEnumerable<Block> blocks)
        {
            allBlocks = blocks;
        }

        public IEnumerable<Block> GetAllBlocks() => allBlocks;
    }
}