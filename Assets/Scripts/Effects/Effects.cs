﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace SpaceOrigin.SpaceInvaders
{
    public enum EffectsType
    {
        AlianExplodeEffect, // enum must be the same name as gameobject, will later change factory implementation
        PlayerbulletExplode,
        PlayerExplode,
        BossExplode
    }

    public class Effects : MonoBehaviour
    {
        public EffectsType m_effectsType;

        public void DestroyAfterSomeTime(float time)
        {
            Invoke("DestroyEffect", time);
        }

        private void DestroyEffect()
        {
            gameObject.SetActive(true); 
            SpaceInvaderAbstractFactory spaceInvaderFactory = SpaceInvaderFactoryProducer.GetFactory("EffectsFactory");
            spaceInvaderFactory.RecycleEffect(this);
        }

    }
}
