'''
VL Agent Engine - Main Loop

메인 로직 함수인 ai_vl_engine_run을 포함
'''

from ai_vl_agent_types import (
    AgentEvent,
    append_think_log,
    PHASE_OBSERVE,
)

def ai_vl_engine_run(
    image,
    think_log=[],
    agent_state={},
    query=None,
    memory=None,
    is_canceled=False,
    verbose=False,
    scenario_name='BASkip'
):
    # verbose 모드 전역 설정 (scanner 함수들에서 참조)
    import ai_vl_engine_scanner
    import ai_vl_engine_images
    ai_vl_engine_scanner.verbose_mode = verbose
    ai_vl_engine_images.verbose_mode = verbose  # NEW!
    
    # 시나리오별 분기
    if scenario_name == 'BASkip':
        from ai_vl_scenario_identify_BASkip import identify_scenario
        from ai_vl_scenario_action_BASkip import get_action, validate_scenario
    elif scenario_name == 'BAReader':
        from ai_vl_scenario_identify_BARead import identify_scenario
        from ai_vl_scenario_action_BARead import get_action, validate_scenario
    else:
        # 지원하지 않는 시나리오
        yield AgentEvent(
            kind='fail',
            message=f'지원하지 않는 시나리오: {scenario_name}',
            think_log=think_log.copy(),
            data={'scenario_name': scenario_name, 'supported': ['BASkip', 'BAReader']}
        )
        return
    
    # 진행 상태: 시나리오 식별 중
    yield AgentEvent(
        kind='thinking',
        message='시나리오 식별 중...',
        think_log=think_log.copy(),
        data={}
    )
    
    # 시나리오 식별 (expected_state 우선 검증)
    current_scenario = identify_scenario(
        image,
        expected_state=agent_state.get('expected_state'),
        # scenario_name='BAMomotalkSkip'
    )
    append_think_log(think_log, PHASE_OBSERVE, f'현재 시나리오: {current_scenario}')

    # 시나리오 검증 (식별 실패 처리)
    validation_event = validate_scenario(current_scenario, agent_state, think_log)
    if validation_event:
        # 검증 실패: 이벤트 반환 (observe 또는 done with alert)
        yield validation_event
        return

    # 진행 상태: 액션 선택 중
    yield AgentEvent(
        kind='thinking',
        message=f'액션 선택 중 (시나리오: {current_scenario})...',
        think_log=think_log.copy(),
        data={'scenario': current_scenario}
    )

    # 현재 시나리오 기반 액션 가져오기 (AgentEvent 반환)
    event = get_action(current_scenario, agent_state, image, think_log)
    
    # AgentEvent 그대로 yield
    yield event
    return
