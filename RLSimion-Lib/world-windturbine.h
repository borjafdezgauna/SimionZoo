#pragma once

#include "world.h"

class CSetPoint;

//ONE-MASS WIND TURBINE MODEL
class CWindTurbine: public CDynamicModel
{
	double m_initial_torque;
	double m_initial_blade_angle;

	CSetPoint *m_pWindData;
	CSetPoint *m_pPowerSetpoint;

public:
	CWindTurbine(CParameters* pParameters);
	~CWindTurbine();

	void reset(CState *s);
	void executeAction(CState *s,CAction *a,double dt);
};