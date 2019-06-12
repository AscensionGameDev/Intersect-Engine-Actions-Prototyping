﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Intersect.Enums;

namespace Intersect.Network.Packets.Server
{
    public class EntityAttackPacket : CerasPacket
    {
        public Guid Id { get; set; }
        public EntityTypes Type { get; set; }
        public Guid MapId { get; set; }
        public int AttackTimer { get; set; }

        public EntityAttackPacket(Guid id, EntityTypes type, Guid mapId, int attackTimer)
        {
            Id = id;
            Type = type;
            MapId = mapId;
            AttackTimer = attackTimer;
        }
    }
}