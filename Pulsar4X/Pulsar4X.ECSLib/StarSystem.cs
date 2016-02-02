﻿using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Pulsar4X.ECSLib
{
    [JsonObject(MemberSerialization.OptIn)]
    public class StarSystem
    {
        [PublicAPI]
        public Guid Guid { get; protected set; }

        [PublicAPI]
        public int Seed
        {
            get { return _seed; }
        }
        [JsonProperty]
        private readonly int _seed;

        [PublicAPI]
        public NameDB NameDB
        {
            get { return _nameDB; }
        }
        [JsonProperty]
        private readonly NameDB _nameDB;

        internal Random RNG { get; private set; }

        internal int EconLastTickRun
        {
            get { return _econLastTickRun; }
            set { _econLastTickRun = value; }
        }
        [JsonProperty]
        private int _econLastTickRun;

        public EntityManager SystemManager
        {
            get { return _systemManager; }
        }

        [JsonProperty("SystemManager")]
        private readonly EntityManager _systemManager;

        [JsonConstructor]
        internal StarSystem()
        {
        }

        public StarSystem(Game game, string name, int seed)
        {
            Guid = Guid.NewGuid();
            _systemManager = new EntityManager(game);
            _nameDB = new NameDB(name);
            _seed = seed;
            game.StarSystems.Add(Guid, this);
            RNG = new Random(seed);
            EconLastTickRun = 0;
        }
    }
}
