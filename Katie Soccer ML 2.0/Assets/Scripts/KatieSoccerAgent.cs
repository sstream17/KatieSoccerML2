using Unity.MLAgents;
using System.Collections;
using TMPro;
using UnityEngine;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Policies;

public class KatieSoccerAgent : Agent
{
    public KatieSoccerAcademy Academy;
    public KatieSoccerAgent OpposingAgent;
    public GameObject[] TeamPieces;
    public GameObject[] OpposingPieces;
    public TextMeshProUGUI Score;

    public GameObject Goal;

    public GameObject Ball;

    public AIGoal GoalDetect;

    private Rigidbody[] teamRBs;
    public bool AllowShot = false;

    private GameObject[] allPieces;
    private float goalReward = 100f;
    private float minStrength = 0.9f;
    private float maxStrength = 5f;
    private float speed = 200f;
    public float OffsetX = 0f;
    public float OffsetY = 0f;
    private float minSpawnX = -4.25f;
    private float maxSpawnX = 4.25f;
    private float minX = -5.9f;
    private float maxX = 5.9f;
    private float normX = 0.5f;
    private float minSpawnY = -3.9f;
    private float maxSpawnY = 2.1f;
    private float minY = -4.1f;
    private float maxY = 2.3f;
    private float normY = 0.640625f;
    private float ballZ = 1.65f;
    private float pieceZ = 1.7f;

    private void Start()
    {
        teamRBs = new Rigidbody[TeamPieces.Length];
        allPieces = new GameObject[TeamPieces.Length + OpposingPieces.Length + 1];
        int i;
        int j = 0;
        for (i = 0; i < TeamPieces.Length; i++)
        {
            GameObject piece = TeamPieces[i];
            Rigidbody rb = piece.GetComponent<Rigidbody>();
            teamRBs[i] = rb;
            allPieces[j] = piece;
            j++;
        }

        for (i = 0; i < OpposingPieces.Length; i++)
        {
            GameObject piece = OpposingPieces[i];
            allPieces[j] = piece;
            j++;
        }

        allPieces[j] = Ball;
    }

    void Update()
    {
        if (AllowShot)
        {
            AllowShot = false;
            RequestDecision();
        }

        if (Score != null)
        {
            Score.text = GetCumulativeReward().ToString("f3");
        }
    }

    public override void OnEpisodeBegin()
    {
        ResetBall();

        foreach (GameObject piece in TeamPieces)
        {
            Rigidbody rb = piece.GetComponent<Rigidbody>();
            rb.velocity = Vector3.zero;
            float offset = transform.position.z + pieceZ;
            piece.transform.position = GetRandomSpawnPos(offset);
            PieceMovement pieceMovement = piece.gameObject.GetComponent<PieceMovement>();
            pieceMovement.SetStartingPositions();
        }

        foreach (GameObject piece in OpposingPieces)
        {
            Rigidbody rb = piece.GetComponent<Rigidbody>();
            rb.velocity = Vector3.zero;
            float offset = transform.position.z + pieceZ;
            piece.transform.position = GetRandomSpawnPos(offset);
            PieceMovement pieceMovement = piece.gameObject.GetComponent<PieceMovement>();
            pieceMovement.SetStartingPositions();
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        var behaviorParameters = gameObject.GetComponent<BehaviorParameters>();

        for (int i = 0; i < TeamPieces.Length; i++)
        {
            var observation = new Vector2(0f, 0f);

            if (TeamPieces[i] != null)
            {
                observation = NormalizePosition(TeamPieces[i].transform.position, behaviorParameters.TeamId);
            }

            sensor.AddObservation(observation);
        }

        for (int i = 0; i < OpposingPieces.Length; i++)
        {
            var observation = new Vector2(0f, 0f);

            if (OpposingPieces[i] != null)
            {
                observation = NormalizePosition(OpposingPieces[i].transform.position, GetOpposingTeamId(behaviorParameters.TeamId));
            }

            sensor.AddObservation(observation);
        }

        var ballObservation = NormalizePosition(Ball.transform.position, behaviorParameters.TeamId);
        sensor.AddObservation(ballObservation);
    }

    public override void OnActionReceived(float[] vectorAction)
    {
        for (int i = 0; i < vectorAction.Length; i++)
        {
            vectorAction[i] = Mathf.Clamp(vectorAction[i], -1f, 1f);
        }

        float magnitude = ScaleAction(vectorAction[0], minStrength, maxStrength);
        float direction = ScaleAction(vectorAction[1], 0f, 2 * Mathf.PI);
        int selectedPiece = Mathf.FloorToInt(ScaleAction(vectorAction[2], 0f, TeamPieces.Length - 0.01f));

        Vector3 targetVector = GetTargetVector(magnitude, direction);
        teamRBs[selectedPiece].AddForce(targetVector * speed);

        // Penalty given each step to encourage agent to finish task quickly.
        AddReward(-1f / Academy.TimePenalty);
    }

    private int GetOpposingTeamId(int thisTeamId)
    {
        return thisTeamId == 1 ? 2 : 1;
    }

    private Vector2 NormalizePosition(Vector2 position, int teamId)
    {
        var direction = teamId == 2 ? 1 : -1;
        float x = (direction * (position.x / (minX - maxX))) + normX;
        float y = (position.y / (minY - maxY)) + normY;
        return new Vector2(x, y);
    }

    private Vector3 GetTargetVector(float magnitude, float direction)
    {
        float x = magnitude * Mathf.Cos(direction);
        float y = magnitude * Mathf.Sin(direction);

        return new Vector3(x, y, 0f);
    }

    public IEnumerator ComputeDistanceScore()
    {
        PieceMovement ballMovement = Ball.GetComponent<PieceMovement>();
        var lastDistance = (Ball.transform.position - Goal.transform.position).magnitude;
        while (ballMovement.IsMoving)
        {
            var distanceToGoal = (Ball.transform.position - Goal.transform.position).magnitude;
            if (distanceToGoal < lastDistance)
            {
                var score = (distanceToGoal * goalReward) + 1;
                AddReward(1 / score);
            }

            lastDistance = distanceToGoal;
            yield return new WaitForFixedUpdate();
        }
    }

    public Vector3 GetRandomSpawnPos(float currentPositionZ)
    {
        float randomPositionX = OffsetX + Random.Range(
            minSpawnX * Academy.spawnAreaMarginMultiplier,
            maxSpawnX * Academy.spawnAreaMarginMultiplier);

        float randomPositionY = OffsetY + Random.Range(
            minSpawnY * Academy.spawnAreaMarginMultiplier,
            maxSpawnY * Academy.spawnAreaMarginMultiplier);

        Vector3 randomSpawnPos = new Vector3(randomPositionX, randomPositionY, currentPositionZ);
        return randomSpawnPos;
    }

    /// <summary>
    /// Called when the agent moves the block into the goal.
    /// </summary>
    public void GoalScored()
    {
        AddReward(goalReward);

        // By marking an agent as done AgentReset() will be called automatically.
        EndEpisode();
    }

    public void OpponentScored()
    {
        AddReward(-goalReward);

        EndEpisode();
    }

    /// <summary>
    /// Resets the block position and velocities.
    /// </summary>
    void ResetBall()
    {
        // Get a random position for the block.
        float offset = transform.position.z + ballZ;
        Ball.transform.position = GetRandomSpawnPos(offset);
    }
}
