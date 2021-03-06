﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaceOrigin.Utilities;
using SpaceOrigin.Data;

namespace SpaceOrigin.SpaceInvaders
{
    /// <summary>
    /// controls the entire invader creation, movement and firing of bullets
    /// </summary>
    public class InvadersManager : MonoBehaviour
    {
        #region public variables
        public float m_gridCellWidth; //  width of the cell
        public float m_gridCellHeight;//  height of the cell
        public Transform m_gridRootObject; // top start position of invaders
        public int m_invaderRowLength = 11; // 11 elements in a row
        public InvaderTypes[] m_invaderTypeRow; // used for initialialy creating orderd grid
        public IntSO m_gameScoreSO; //  update the score on every invader kill
        public IntSO m_totalAliveInvadersSO; // so objects holds the alive invaders, 0 means game over. using for avoid depencdency
        public Transform m_bossSpawnPoint; // where the boss start moving
        public AudioClip m_invaderExlodeClip;

        #endregion

        #region private variables
        private int m_gridRows; // number of raws inside the cell
        private int m_gridColumns; // number of columns inside the cell
        private Grid2D m_2DGrid; // grid for initializing positions of invaders
        private Invader[][] m_invaderRows; // jaggy arry for storing invaders
        private int m_totalAliveInviders;
        private float m_invaderHorMovDirection = 1;// 1 right -1 left
        private float m_horizonatalVeleocity = .05f; // amount movement on the horizontal Axis //.05f
        private float m_verticalVeleocity = .25f; // amount movement on the vertical Axis //0.25f
        private InvaderGridState m_invaderGridState;
        private float m_lastMoveUpdateTime;
        private float m_moveUpdateInterval = .5f;
        private RawCoumn m_leftMaxMoveCheckIndex; // index for checking Left boundary
        private RawCoumn m_rightMaxMoveCheckIndex; // index for checking right boundary

        // fire
        private float m_lastInvaderBulletTime;// when was the last time that you fired the bullet
        private float m_invaderBulletInterval = 1.2f; //interval betwnn the bullets.
        private int m_lastFiredColumn;
        private Invader[] m_firableColumn;
        private bool m_firableColumnReady;

        //boss
        // if you kill  2 invaders of same column in row, that triggers a boss + also must be gained 150 more points from the last boss appearance
        // also there is boss timer
        // when the the eniemies are less timer dcreases
        private int m_lastInviderColumnIndex;
        private int m_lastInviderColumnIndexCntr = -1; // counts the  row
        private int m_maxInvaderColumnCntr = 2; // how many invaders user need to kill in row to get a boss 
        private int m_lastScoreBossShown;// last score when boss shoewed up
        private int m_minScoreNeeded = 150;// min score gap from the lass boss

        // counter based boss
        private float m_lastBossShownTime;
        private float m_bossInterval = 20.0f;
        private Boss m_bossInvader = null;

        //audio
        private AudioSource m_audioSource;
        #endregion

        #region struct 
        public struct RawCoumn
        {
            public int m_rawIndex;
            public int m_columnIndex;

            public RawCoumn(int rawIndex, int columnIndex)
            {
                m_rawIndex = rawIndex;
                m_columnIndex = columnIndex;
            }
        }
        #endregion

        #region enum 
        public enum InvaderGridState  // state pattern
        {
            NotReady,
            Created,
            Displayed,
            Moving,
            Pause // pause the movement
        }
        #endregion

        #region unity events
        void Awake()
        {
            m_audioSource = GetComponent<AudioSource>();
        }

        // Update is called once per frame
        void Update()
        {
            if (m_invaderGridState == InvaderGridState.Moving)
            {
                if (m_lastMoveUpdateTime + m_moveUpdateInterval <= Time.time)// this controlls this speed of invaders
                {
                    StartCoroutine(UpdateInvaderMovementHoriZontal()); // updating horizontal invader moves
                    m_lastMoveUpdateTime = Time.time;
                }

                bool hitOnBoundary = CheckForBoundary(); // checking for boundaries
                if (hitOnBoundary)
                    UpdateInvaderMovementVertical(); // if we reached boundary move down a row


                if (m_firableColumnReady && m_lastInvaderBulletTime + m_invaderBulletInterval <= Time.time) // time to fire another bullet?
                {
                    FireBullet();
                    m_lastInvaderBulletTime = Time.time;
                }

                if (m_bossInvader == null && m_lastBossShownTime + m_bossInterval <= Time.time)
                {
                    Debug.Log("creating new boss : SYSTEM Generated");
                    CreatNewBoss();
                    m_lastBossShownTime = Time.time;
                }
            }
        }
        #endregion

        #region public methodes
        //creates new invader grid and dispay it on the screen with animation
        public void CreateAndShowInvaders(Action onComplete)
        {
            Debug.Log("Creating invaders");
            CreateInvaderGrid();
            StartCoroutine(ShowAllInviders(onComplete));
        }

        public void RemoveBoss()
        {
            m_bossInvader = null;
        }

        public void DestroyBoss(Boss boss)
        {
            SpaceInvaderAbstractFactory spaceInvaderFactory = SpaceInvaderFactoryProducer.GetFactory("EffectsFactory");
            Effects bossExplodeEffect = spaceInvaderFactory.GetEffects(EffectsType.BossExplode);
            bossExplodeEffect.transform.position = boss.transform.position;
            bossExplodeEffect.gameObject.SetActive(true);
            bossExplodeEffect.DestroyAfterSomeTime(.15f);

            spaceInvaderFactory = SpaceInvaderFactoryProducer.GetFactory("BossFactory"); // accessomg boss factoy
            spaceInvaderFactory.RecycleBoss(boss);
            boss.gameObject.SetActive(false);
            m_audioSource.PlayOneShot(m_invaderExlodeClip);
            m_bossInvader = null;

            // score
            // for now, if the inviders move right it is 100, 50 left move not really a mystery :(
            int bossKillScore = m_invaderHorMovDirection > 0 ? 100 : 50;
            m_gameScoreSO.Value = m_gameScoreSO.Value + bossKillScore;
        }

        // called when player bullet hit on invader. check invader script
        public void DestroyInvader(Invader thisInvader) 
        {
            // for boss checking
            if (m_lastInviderColumnIndexCntr < 0)
            {
                m_lastInviderColumnIndex = thisInvader.m_coumnIndex; m_lastInviderColumnIndexCntr = 1;
            }
            else
            {
                int cntr = m_lastInviderColumnIndex == thisInvader.m_coumnIndex ? m_lastInviderColumnIndexCntr + 1 : 1; // adding to the colmns index 
                m_lastInviderColumnIndexCntr = cntr;
                m_lastInviderColumnIndex = thisInvader.m_coumnIndex;
            }

            bool timetoBoSS = m_lastInviderColumnIndexCntr >= m_maxInvaderColumnCntr ? true : false;
            if (timetoBoSS && m_lastScoreBossShown + m_minScoreNeeded < m_gameScoreSO.Value)
            {
                if (m_bossInvader == null)
                {
                    m_lastScoreBossShown = m_gameScoreSO.Value;
                    CreatNewBoss();
                    Debug.Log("creating new boss : USER Generated");
                    m_lastBossShownTime = Time.time;
                } 
            }

            m_totalAliveInviders--; // decrement the invader count
            m_totalAliveInvadersSO.Value = m_totalAliveInviders;
            m_gameScoreSO.Value = m_gameScoreSO.Value + thisInvader.m_killValue.Value;
            Vector3 invaderLastPos = thisInvader.transform.position;

            m_invaderRows[thisInvader.m_rowIndex][thisInvader.m_coumnIndex] = null;
            SpaceInvaderAbstractFactory spaceInvaderFactory = SpaceInvaderFactoryProducer.GetFactory("InvaderFactory"); // accessomg InvaderFactory
            spaceInvaderFactory.RecycleInvader(thisInvader);

            spaceInvaderFactory = SpaceInvaderFactoryProducer.GetFactory("EffectsFactory");
            Effects invadeExplodeEffect = spaceInvaderFactory.GetEffects(EffectsType.AlianExplodeEffect);

            invadeExplodeEffect.transform.position = invaderLastPos;
            invadeExplodeEffect.gameObject.SetActive(true);
            invadeExplodeEffect.DestroyAfterSomeTime(.15f);
            m_audioSource.PlayOneShot(m_invaderExlodeClip);

            UpdateTimeIntervalOFMove(); // check against the number of inviders left and increas the move speed 

            if (m_totalAliveInviders == 0) //  next wave
            {
                m_invaderGridState = InvaderGridState.Pause;
                return;
                // for now there wont be any new waves, game will be over at this point
                // wil add new wave feature in the next update
            }
        }
        public void SetStateToPause()
        {
            m_invaderGridState = InvaderGridState.Pause;
        }
        #endregion

        #region private methodes
        // initializes inivader manger
        private void Initialize()
        {
            m_totalAliveInviders = 0;
            m_invaderGridState = InvaderGridState.NotReady;
            m_moveUpdateInterval = .5f;
        }

        // updates the invader row movement speed based on no of invaders alive
        private void UpdateTimeIntervalOFMove()
        {
            if (m_totalAliveInviders <= 1)
            {
                m_moveUpdateInterval = .02f;
            }
            else if (m_totalAliveInviders <= 3)
            {
                m_moveUpdateInterval = .04f;
            }
            else if (m_totalAliveInviders <= 5)
            {
                m_bossInterval = 15.0f; // more frequent bosses
                m_moveUpdateInterval = .06f;
            }
            else if (m_totalAliveInviders <= 10)
            {
                m_moveUpdateInterval = .1f;
            }
            else if (m_totalAliveInviders <= 20)
            {
                m_moveUpdateInterval = .3f;
            }
        }

        // creates the Invaders in a grid manner
        // with the help of Grid class, this creates a new invader grids and stores in an jagged array
        private void CreateInvaderGrid()
        {
            if (m_invaderTypeRow.Length < 1) return;

            m_gridRows = m_invaderTypeRow.Length; // number of rows of invaders
            m_gridColumns = m_invaderRowLength;  // row counts is basically max Coulumn

            m_leftMaxMoveCheckIndex = new RawCoumn(0, 0); // first row firt column
            m_rightMaxMoveCheckIndex = new RawCoumn(0, m_gridColumns -1); // first row last column

            m_invaderRows = new Invader[m_gridRows][];
            for (int i = 0; i < m_gridRows; i++)
            {
                m_invaderRows[i] = new Invader[m_gridColumns];
            }

            m_2DGrid = new Grid2D(m_gridRows, m_gridColumns, m_gridCellWidth, m_gridCellHeight, m_gridRootObject.position);
            SpaceInvaderAbstractFactory spaceInvaderFactory = SpaceInvaderFactoryProducer.GetFactory("InvaderFactory"); // accessomg InvaderFactory

            for (int i = 0; i < m_gridRows; i++)
            {
                InvaderTypes rowType = m_invaderTypeRow[i]; // current row type
                for (int j = 0; j < m_gridColumns; j++)
                {
                    Cell cell = m_2DGrid.Cells[i][j];

                    Invader invader = spaceInvaderFactory.GetInvader(rowType); // asking for type of invaders from the factory
                    invader.gameObject.transform.position = cell.m_center;

                    invader.m_invaderManger = this;
                    invader.m_rowIndex = i;
                    invader.m_coumnIndex = j;
                    m_totalAliveInviders++;
                    m_totalAliveInvadersSO.Value = m_totalAliveInviders;
                    m_invaderRows[i][j] = invader; // assigning to the invader array 
                }
            }

            m_firableColumn = m_invaderRows[m_invaderRows.Length - 1];
            m_invaderGridState = InvaderGridState.Created;
            // Invaders will be in hidden state at this point and next ShowAllInviders will display invaders with animation
        }

        // Showing invaders with animation
        private IEnumerator ShowAllInviders(Action onComplete)
        {
            for (int i = m_invaderRows.Length - 1; i >= 0; i--) // start looping from bottom left
            {
                Invader[] invaderRow = m_invaderRows[i];// current row 
                for (int j = 0; j < m_gridColumns; j++)
                {
                    Invader invader = invaderRow[j];
                    invader.gameObject.SetActive(true); // start vible to camera

                    yield return new WaitForSeconds(.04f); // slight delay for the animations
                }
            }
            m_invaderGridState = InvaderGridState.Displayed;
            yield return new WaitForSeconds(.2f);

            m_invaderHorMovDirection = 1; // setting moving directio as right
            m_invaderGridState = InvaderGridState.Moving;
            m_lastBossShownTime = Time.time;

            yield return null;

            onComplete?.Invoke();
        }
       
        //Moves Invader in horizontal Axis
        private IEnumerator UpdateInvaderMovementHoriZontal()
        {
            m_firableColumnReady = false;
            Invader[] firableColumn = new Invader[m_invaderRows[0].Length];

            // we also calulate the firing coumn here to avoid another itration

            for (int i = m_invaderRows.Length - 1; i >= 0; i--)
            {
                Invader[] invaderRow = m_invaderRows[i];
                for (int j = 0; j < m_gridColumns; j++)
                {
                    Invader invader = invaderRow[j];
                    if (invader != null)
                    {
                        Vector3 horizontalDeltaMove = new Vector3( m_invaderHorMovDirection* m_horizonatalVeleocity, 0, 0); // calculate new move delta
                        invader.Move(horizontalDeltaMove);
                        invader.PlayAnimation();

                        if (firableColumn[j] == null) firableColumn[j] = invader;
                    }
                }

                yield return new WaitForSeconds(m_moveUpdateInterval / m_invaderRows.Length);
            }

            m_firableColumn = firableColumn;
            m_firableColumnReady = true;
            yield return null;
        }

        // Moves Invader in vertical down Axis when the horizonatl move reach on the boundary
        private void UpdateInvaderMovementVertical()
        {
            for (int i = m_invaderRows.Length - 1; i >= 0; i--)
            {
                Invader[] invaderRow = m_invaderRows[i];
                for (int j = 0; j < m_gridColumns; j++)
                {
                    Invader invader = invaderRow[j];
                    if (invader != null)
                    {
                        Vector3 verticallDeltaMove = new Vector3(0, -m_verticalVeleocity*1.0f, 0);
                        invader.Move(verticallDeltaMove);
                        invader.PlayAnimation();
                    }
                }
            }
        }

        // checking for boundary, if find, then return true and also change the move dirextion
        private bool CheckForBoundary()
        {
            // first we need to find the max left and max right invader
            Invader[] invaderRow = m_invaderRows[0]; // we condier always the top row
            bool foundLeftMax = false;
            bool foundRightMax = false;

            for (int i = 0; i < invaderRow.Length; i++)
            {
                if(!foundLeftMax && invaderRow[i] != null) // starting from left for finding left max
                {
                    m_leftMaxMoveCheckIndex.m_rawIndex = 0;
                    m_leftMaxMoveCheckIndex.m_columnIndex = i;
                    foundLeftMax = true;
                }

                if (!foundRightMax && invaderRow[invaderRow.Length -1 - i] != null) // starting from back for finding right max
                {
                    m_rightMaxMoveCheckIndex.m_rawIndex = 0;
                    m_rightMaxMoveCheckIndex.m_columnIndex = invaderRow.Length - 1 - i;
                    foundRightMax = true;
                }
            }

            // checking against right bounds
            if (m_invaderHorMovDirection > 0)
            {
                Invader rightMaxInvader = m_invaderRows[m_rightMaxMoveCheckIndex.m_rawIndex][m_rightMaxMoveCheckIndex.m_columnIndex];
                if (rightMaxInvader != null && rightMaxInvader.transform.position.x >= SpaceBoundary.Instance.RightMax)
                {
                    m_invaderHorMovDirection = -1;
                    return true;
                }
            }

            // checking against left bounds
            if (m_invaderHorMovDirection < 0)
            {
                Invader leftMaxInvader = m_invaderRows[m_leftMaxMoveCheckIndex.m_rawIndex][m_leftMaxMoveCheckIndex.m_columnIndex];
                if (leftMaxInvader != null && leftMaxInvader.transform.position.x <= SpaceBoundary.Instance.LeftMax)
                {
                    m_invaderHorMovDirection = 1;
                    return true;
                }
            }
            return false;
        }

        // very basic implementation of firing mechnism, it can be more advanced if needed which I feel not needed for this game
        private void FireBullet()
        {
            int newFireColumn = 0;
            List<int> nonEmptyFirableColumnIndexs = new List<int>();

            for (int i = 0; i < m_firableColumn.Length; i++)
            {
                if (m_lastFiredColumn != i && m_firableColumn[i] != null)
                {
                    nonEmptyFirableColumnIndexs.Add(i);
                }
            }

            newFireColumn = nonEmptyFirableColumnIndexs[UnityEngine.Random.Range(0, nonEmptyFirableColumnIndexs.Count)];

            Invader fireInvader = m_firableColumn[newFireColumn];
            Vector3 firePosition = fireInvader.transform.position;

            SpaceInvaderAbstractFactory spaceInvaderFactory = SpaceInvaderFactoryProducer.GetFactory("InvaderBulletFactory"); // accessomg InvaderFactory
            InvaderBullet invaderBullet =  spaceInvaderFactory.GetInvaderBullet(InvaderBulletTypes.SquiglyShot);

            invaderBullet.gameObject.transform.position = new Vector3(firePosition.x, firePosition.y - .3f, firePosition.z);
            invaderBullet.gameObject.transform.rotation = Quaternion.identity;
            invaderBullet.gameObject.SetActive(true);
        }

        private void CreatNewBoss()
        {
            SpaceInvaderAbstractFactory spaceInvaderFactory = SpaceInvaderFactoryProducer.GetFactory("BossFactory"); // accessomg InvaderFactory                                                                                            
            Boss boss = spaceInvaderFactory.GetBoss();
            boss.m_invaderManger = this;
            Vector3 spawnPosition = m_bossSpawnPoint.position;

            if (m_invaderHorMovDirection > 0)
            {
                boss.m_directionMove = m_invaderHorMovDirection; // boss always move in the same direction of invaders
                boss.transform.gameObject.transform.position = new Vector3(-3.5f, spawnPosition.y, spawnPosition.z);
            }
            else
            {
                boss.m_directionMove = m_invaderHorMovDirection;
                boss.transform.gameObject.transform.position = new Vector3(3.5f, spawnPosition.y, spawnPosition.z);
            }
            boss.transform.gameObject.SetActive(true);
            m_bossInvader = boss;
            boss.PlaySound();
        }

        #endregion
    }

}
