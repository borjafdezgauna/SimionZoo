#pragma once

#include "CNTKLibrary.h"
#include "../Lib/deep-network.h"
#include "../Lib/deep-layer.h"
#include <string>
using namespace std;

class DeepNetworkDefinition;

class CntkNetwork
{
protected:
	vector<string> m_inputStateVariables;
	vector<string> m_inputActionVariables;
	size_t m_numOutputs;
	string m_networkLayersDefinition;
	string m_learnerDefinition;

	const wstring m_stateInputVariableId = L"input-state";
	const wstring m_actionInputVariableId = L"input-action";
	const wstring m_targetVariableId = L"target";
	const wstring m_networkOutputId = L"output";
	const wstring m_lossVariableId = L"loss-variable";
	const wstring m_networkLearnerCombinationId = L"network-learner";
	const wstring m_fullNetworLearnerFunctionId = L"full-network-learner";

	CNTK::Variable m_inputState;
	CNTK::Variable m_targetVariable;
	CNTK::FunctionPtr m_networkOutput;
	CNTK::FunctionPtr m_fullNetworkLearnerFunction;
	CNTK::TrainerPtr m_trainer;

	CNTK::TrainerPtr trainer(CNTK::FunctionPtr networkOutput, CNTK::FunctionPtr lossFunction, CNTK::FunctionPtr evalFunction, string learnerDefinition);
	CNTK::FunctionPtr mergeLayer(CNTK::Variable var1, CNTK::Variable var2);
	CNTK::FunctionPtr activationFunction(CNTK::FunctionPtr input, Activation activationFunction);
	CNTK::FunctionPtr denseLayer(CNTK::FunctionPtr layerInput, size_t outputDim);
	CNTK::FunctionPtr initNetworkLayers(CNTK::FunctionPtr networkInput, string layersDefinition);
	CNTK::FunctionPtr initNetworkLearner(CNTK::FunctionPtr networkOutput, string learnerDefinition);
	CNTK::FunctionPtr initNetworkFromInputLayer(CNTK::FunctionPtr inputLayer);

	//base functionality for networks using only the state as input to avoid replicating it
	void _evaluate(const vector<double>& s, vector<double>& output);
	virtual void _train(DeepMinibatch* pMinibatch, const vector<double>& target);
	void _clone(const CntkNetwork* pSource, bool bFreezeWeights = true);

	//used for single-tuple evaluations
	vector<double>& _evaluate(const State* s);
	vector<double> m_outputBuffer;
	vector<double> m_stateBuffer;
	void stateToVector(const State* s, vector<double>& stateVector);

	CntkNetwork(vector<string> inputStateVariables, vector<string> inputActionVariables
		, size_t numOutputs, string networkLayersDefinition, string learnerDefinition);
public:
	//StateActionFunction methods to be called from subclasses
	unsigned int getNumOutputs();
	const vector<string>& getInputStateVariables();
	const vector<string>& getInputActionVariables();
};

class CntkDiscreteQFunctionNetwork: public IDiscreteQFunctionNetwork, CntkNetwork
{
public:
	CntkDiscreteQFunctionNetwork(vector<string> inputStateVariables, size_t numActionSteps
		, string networkLayersDefinition, string learnerDefinition);
	
	unsigned int getNumOutputs() { return CntkNetwork::getNumOutputs(); }
	const vector<string>& getInputStateVariables() { return CntkNetwork::getInputStateVariables(); }
	const vector<string>& getInputActionVariables() { return CntkNetwork::getInputActionVariables(); }
	void destroy();
	IDeepNetwork* clone(bool bFreezeWeights = true) const;
	void train(DeepMinibatch* pMinibatch, const vector<double>& target);
	void evaluate(const vector<double>& s, vector<double>& output);
	vector<double>& evaluate(const State* s, const Action* a);
};

class CntkContinuousQFunctionNetwork : public IContinuousQFunctionNetwork, CntkNetwork
{
	CNTK::Variable m_inputAction;
	vector<double> m_actionBuffer;
	void actionToVector(const Action* a, vector<double>& stateVector);
public:
	CntkContinuousQFunctionNetwork(vector<string> inputStateVariables, vector<string> inputActionVariables
		, string networkLayersDefinition, string learnerDefinition);

	unsigned int getNumOutputs() { return 1; }
	const vector<string>& getInputStateVariables() { return CntkNetwork::getInputStateVariables(); }
	const vector<string>& getInputActionVariables() { return CntkNetwork::getInputActionVariables(); }
	void destroy();
	IDeepNetwork* clone(bool bFreezeWeights = true) const;
	void train(DeepMinibatch* pMinibatch, const vector<double>& target);
	void evaluate(const vector<double>& s, const vector<double>& a, vector<double>& output);
	vector<double>& evaluate(const State* s, const Action* a);
};

class CntkVFunctionNetwork : public IVFunctionNetwork, CntkNetwork
{
public:
	CntkVFunctionNetwork(vector<string> inputStateVariables, string networkLayersDefinition, string learnerDefinition);

	unsigned int getNumOutputs() { return 1; }
	const vector<string>& getInputStateVariables() { return CntkNetwork::getInputStateVariables(); }
	const vector<string>& getInputActionVariables() { return CntkNetwork::getInputActionVariables(); }
	void destroy();
	IDeepNetwork* clone(bool bFreezeWeights = true) const;
	void train(DeepMinibatch* pMinibatch, const vector<double>& target);
	void evaluate(const vector<double>& s, vector<double>& output);
	vector<double>& evaluate(const State* s, const Action* a);
};

class CntkDeterministicPolicyNetwork : public IDeterministicPolicyNetwork, CntkNetwork
{
public:
	CntkDeterministicPolicyNetwork(vector<string> inputStateVariables, string networkLayersDefinition, string learnerDefinition);

	unsigned int getNumOutputs() { return 1; }
	const vector<string>& getInputStateVariables() { return CntkNetwork::getInputStateVariables(); }
	const vector<string>& getInputActionVariables() { return CntkNetwork::getInputActionVariables(); }
	void destroy();
	IDeepNetwork* clone(bool bFreezeWeights = true) const;
	void train(DeepMinibatch* pMinibatch, const vector<double>& target);
	void evaluate(const vector<double>& s, vector<double>& output);
	vector<double>& evaluate(const State* s, const Action* a);
};