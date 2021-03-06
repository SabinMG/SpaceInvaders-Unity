﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceOrigin.SpaceInvaders
{
    public abstract class SpaceInvaderAbstractFactory
    {
        public abstract Invader GetInvader(InvaderTypes type);
        public abstract PlayerBullet GetPlayerBullet();
        public abstract InvaderBullet GetInvaderBullet(InvaderBulletTypes type);
        public abstract Effects GetEffects(EffectsType effectType);
        public abstract Player GetPlayer();
        public abstract Boss  GetBoss();

        public abstract void RecyclePlayerBullet(PlayerBullet playerBullet);
        public abstract void RecycleInvaderBullet(InvaderBullet invaderBullet);
        public abstract void RecycleInvader(Invader invader);
        public abstract void RecycleEffect(Effects effect);
        public abstract void RecyclePlayer(Player player);
        public abstract void RecycleBoss(Boss boss);
    }
}
