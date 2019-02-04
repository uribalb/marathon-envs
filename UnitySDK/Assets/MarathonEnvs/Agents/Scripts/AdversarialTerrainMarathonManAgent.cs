using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MLAgents;
using System.Linq;
using static BodyHelper002;

public class AdversarialTerrainMarathonManAgent : Agent, IOnTerrainCollision
{
	BodyManager002 _bodyManager;

    AdversarialTerrainAgent _adversarialTerrainAgent;
    int _lastXPosInMeters;
    float _pain;
    bool _modeRecover;

	override public void CollectObservations()
	{
		Vector3 normalizedVelocity = _bodyManager.GetNormalizedVelocity();
        var pelvis = _bodyManager.GetFirstBodyPart(BodyPartGroup.Hips);
        var shoulders = _bodyManager.GetFirstBodyPart(BodyPartGroup.Torso);

        AddVectorObs(normalizedVelocity); 
        AddVectorObs(pelvis.Rigidbody.transform.forward); // gyroscope 
        AddVectorObs(pelvis.Rigidbody.transform.up);

        AddVectorObs(shoulders.Rigidbody.transform.forward); // gyroscope 
        AddVectorObs(shoulders.Rigidbody.transform.up);

		AddVectorObs(_bodyManager.GetSensorIsInTouch());
		AddVectorObs(_bodyManager.GetBodyPartsObservations());
		AddVectorObs(_bodyManager.GetMusclesObservations());
		AddVectorObs(_bodyManager.GetSensorYPositions());
		AddVectorObs(_bodyManager.GetSensorZPositions());

		_bodyManager.OnCollectObservationsHandleDebug(GetInfo());

        if (_adversarialTerrainAgent == null)
            _adversarialTerrainAgent = GetComponent<AdversarialTerrainAgent>();
        
        (List<float> distances, float fraction) = 
            _adversarialTerrainAgent.GetDistances2d(
                pelvis.Rigidbody.transform.position, _bodyManager.ShowMonitor);
    
        AddVectorObs(distances);
        AddVectorObs(fraction);

        _lastXPosInMeters = (int) 
            _bodyManager.GetBodyParts(BodyPartGroup.Foot)
            .Average(x=>x.Transform.position.x);
        _adversarialTerrainAgent.Terminate(GetCumulativeReward());
	}

	public override void AgentAction(float[] vectorAction, string textAction)
	{
		// apply actions to body
		_bodyManager.OnAgentAction(vectorAction, textAction);

		// manage reward
        float velocity = Mathf.Clamp(_bodyManager.GetNormalizedVelocity().x, 0f, 1f);
		var actionDifference = _bodyManager.GetActionDifference();
		var actionsAbsolute = vectorAction.Select(x=>Mathf.Abs(x)).ToList();
		var actionsAtLimit = actionsAbsolute.Select(x=> x>=1f ? 1f : 0f).ToList();
		float actionaAtLimitCount = actionsAtLimit.Sum();
        float notAtLimitBonus = 1f - (actionaAtLimitCount / (float) actionsAbsolute.Count);
        float reducedPowerBonus = 1f - actionsAbsolute.Average();

		// velocity *= 0.85f;
		// reducedPowerBonus *=0f;
		// notAtLimitBonus *=.1f;
		// actionDifference *=.05f;
        // var reward = velocity
		// 				+ notAtLimitBonus
		// 				+ reducedPowerBonus
		// 				+ actionDifference;		
        var pelvis = _bodyManager.GetFirstBodyPart(BodyPartGroup.Hips);
		if (pelvis.Transform.position.y<0){
			Done();
		}
		

        var reward = velocity;

		AddReward(reward);
		_bodyManager.SetDebugFrameReward(reward);
	}


	public override void AgentReset()
	{
		if (_bodyManager == null)
			_bodyManager = GetComponent<BodyManager002>();
		_bodyManager.OnAgentReset();
	}
	public virtual void OnTerrainCollision(GameObject other, GameObject terrain)
	{
		// if (string.Compare(terrain.name, "Terrain", true) != 0)
		if (terrain.GetComponent<Terrain>() == null)
			return;
		// if (!_styleAnimator.AnimationStepsReady)
		// 	return;
		var bodyPart = _bodyManager.BodyParts.FirstOrDefault(x=>x.Transform.gameObject == other);
		if (bodyPart == null)
			return;
		switch (bodyPart.Group)
		{
			case BodyHelper002.BodyPartGroup.None:
			case BodyHelper002.BodyPartGroup.Foot:
			// case BodyHelper002.BodyPartGroup.LegUpper:
			case BodyHelper002.BodyPartGroup.LegLower:
			case BodyHelper002.BodyPartGroup.Hand:
			// case BodyHelper002.BodyPartGroup.ArmLower:
			// case BodyHelper002.BodyPartGroup.ArmUpper:
				break;
			default:
				// AddReward(-100f);
				if (!IsDone()){
					Done();
                    _adversarialTerrainAgent.Terminate(GetCumulativeReward());
				}
				break;
		}
	}
}
