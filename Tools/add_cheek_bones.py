# -*- coding: utf-8 -*-
# Blender에서 볼(cheek) 본 2개(Character_Ball_L / Character_Ball_R)를 추가하는 헬퍼.
#
# 사용법:
#   1) Blender에서 대상 캐릭터 .fbx / .blend 를 연다.
#   2) Scripting 탭 > 새 텍스트 > 이 파일 붙여넣기.
#   3) 아래 CONFIG 값을 캐릭터에 맞게 수정한다.
#   4) Run Script.
#   5) Weight Paint 모드에서 볼 정점 가중치를 다듬는다(자동 할당은 초안일 뿐).
#   6) FBX로 Export (아래 "Export 주의" 참고).
#
# 주의: 실행 전 .blend 를 백업할 것. 되돌리기가 어렵다.

import bpy
import mathutils

# ===================== CONFIG =====================
ARMATURE_NAME = "Armature"      # 아마추어(스켈레톤) 오브젝트 이름
HEAD_BONE_NAME = "Head"         # 볼 본을 붙일 부모 본(머리). 캐릭터에 맞게 수정
MESH_NAME = ""                  # 얼굴 메시 오브젝트 이름. 비우면 아마추어의 첫 스킨 메시 자동 사용

# 볼 본 끝점을 만들 상대 오프셋(머리 본 로컬 기준, 미터). 캐릭터 스케일에 맞게 조정
# X: 좌우, Y/Z: Blender 축계에 따라 다름 → Run 후 위치 눈으로 확인하며 조정
CHEEK_OFFSET_L = mathutils.Vector(( 0.06, 0.02, 0.03))   # 왼쪽 볼
CHEEK_OFFSET_R = mathutils.Vector((-0.06, 0.02, 0.03))   # 오른쪽 볼
BONE_LENGTH = 0.02             # 본 길이(표시용)

# 자동 가중치: 각 볼 본 head 위치 반경 내 정점에 가중치 부여(초안)
AUTO_WEIGHT = True
WEIGHT_RADIUS = 0.05           # 이 반경 안의 정점에 가중치(미터)
WEIGHT_MAX = 1.0               # 중심 최대 가중치(가장자리로 갈수록 0)
# ==================================================


def get_armature():
    arm = bpy.data.objects.get(ARMATURE_NAME)
    if arm is None or arm.type != 'ARMATURE':
        raise RuntimeError("아마추어를 찾을 수 없음: %s" % ARMATURE_NAME)
    return arm


def get_mesh(arm):
    # 명시된 메시가 있으면 사용
    if MESH_NAME:
        m = bpy.data.objects.get(MESH_NAME)
        if m is None or m.type != 'MESH':
            raise RuntimeError("메시를 찾을 수 없음: %s" % MESH_NAME)
        return m
    # 없으면 이 아마추어를 쓰는 첫 스킨 메시 사용
    for obj in bpy.data.objects:
        if obj.type != 'MESH':
            continue
        for mod in obj.modifiers:
            if mod.type == 'ARMATURE' and mod.object == arm:
                return obj
    raise RuntimeError("아마추어에 연결된 스킨 메시를 자동으로 찾지 못함. MESH_NAME을 지정하라.")


def add_bone(arm, bone_name, head_pos, tail_pos, parent_name):
    # Edit 모드에서 본 추가
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT')

    edit_bones = arm.data.edit_bones

    # 이미 있으면 재사용
    if bone_name in edit_bones:
        eb = edit_bones[bone_name]
    else:
        eb = edit_bones.new(bone_name)

    eb.head = head_pos
    eb.tail = tail_pos

    # 부모 본에 연결(오프셋 유지)
    if parent_name in edit_bones:
        eb.parent = edit_bones[parent_name]
        eb.use_connect = False
    else:
        print("[warn] 부모 본 없음: %s (루트로 생성)" % parent_name)

    bpy.ops.object.mode_set(mode='OBJECT')


def assign_auto_weights(arm, mesh, bone_name, center_world):
    # 버텍스 그룹 확보
    vg = mesh.vertex_groups.get(bone_name)
    if vg is None:
        vg = mesh.vertex_groups.new(name=bone_name)

    mw = mesh.matrix_world
    count = 0

    # 반경 내 정점에 거리 기반 가중치 할당
    for v in mesh.data.vertices:
        world_co = mw @ v.co
        dist = (world_co - center_world).length
        if dist <= WEIGHT_RADIUS:
            w = WEIGHT_MAX * (1.0 - (dist / WEIGHT_RADIUS))
            if w > 0.0:
                vg.add([v.index], w, 'REPLACE')
                count += 1

    print("[info] %s 자동 가중치 정점 수: %d" % (bone_name, count))


def main():
    arm = get_armature()
    arm_matrix = arm.matrix_world

    # 부모(머리) 본의 head 위치를 기준점으로 사용
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT')
    if HEAD_BONE_NAME not in arm.data.edit_bones:
        bpy.ops.object.mode_set(mode='OBJECT')
        raise RuntimeError("머리 본을 찾을 수 없음: %s" % HEAD_BONE_NAME)
    head_local = arm.data.edit_bones[HEAD_BONE_NAME].head.copy()
    bpy.ops.object.mode_set(mode='OBJECT')

    # 좌/우 볼 본 head/tail(아마추어 로컬 좌표)
    l_head = head_local + CHEEK_OFFSET_L
    l_tail = l_head + mathutils.Vector((0, 0, BONE_LENGTH))
    r_head = head_local + CHEEK_OFFSET_R
    r_tail = r_head + mathutils.Vector((0, 0, BONE_LENGTH))

    add_bone(arm, "Character_Ball_L", l_head, l_tail, HEAD_BONE_NAME)
    add_bone(arm, "Character_Ball_R", r_head, r_tail, HEAD_BONE_NAME)
    print("[info] 볼 본 2개 추가 완료")

    if AUTO_WEIGHT:
        mesh = get_mesh(arm)
        # 본 head의 월드 좌표 기준으로 가중치
        assign_auto_weights(arm, mesh, "Character_Ball_L", arm_matrix @ l_head)
        assign_auto_weights(arm, mesh, "Character_Ball_R", arm_matrix @ r_head)
        print("[info] 자동 가중치 초안 완료 - Weight Paint에서 반드시 다듬을 것")

    print("=== 끝. Weight Paint로 볼 정점을 정리한 뒤 FBX Export 하세요. ===")


# Export 주의:
#   - File > Export > FBX
#   - Limit to: 필요한 오브젝트만 선택 후 'Selected Objects'
#   - Armature > 'Add Leaf Bones' 끄기 권장
#   - Unity에서 Rig가 Humanoid면, 추가 본은 매핑에서 제외된 'Extra' 본으로 남는다(정상)
main()
