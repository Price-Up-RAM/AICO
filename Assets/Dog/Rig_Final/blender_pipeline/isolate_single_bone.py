'''
"애니메이션 문제인가 웨이트 문제인가" 를 가르는 실험.
애니메이션을 전혀 쓰지 않고 본 하나씩만 N도 회전시켜 엣지 늘어남을 잰다.
단일 본 회전만으로 스파이크가 나면 -> 애니메이션이 아니라 웨이트/바인딩 문제.
'''
import bpy, sys, math, statistics
from collections import Counter
from mathutils import Vector

argv = sys.argv[sys.argv.index("--")+1:]
fbx = argv[0]
ANGLE = math.radians(float(argv[1]) if len(argv) > 1 else 30.0)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.wm.fbx_import(filepath=fbx)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
mesh = max((o for o in bpy.data.objects if o.type == 'MESH'), key=lambda o: len(o.data.vertices))
if arm.animation_data: arm.animation_data.action = None
for pb in arm.pose.bones: pb.rotation_mode = 'QUATERNION'

edges = [(e.vertices[0], e.vertices[1]) for e in mesh.data.edges]
groups = {g.index: g.name for g in mesh.vertex_groups}

def deformed():
    dg = bpy.context.evaluated_depsgraph_get(); ev = mesh.evaluated_get(dg)
    me = ev.to_mesh(); m = ev.matrix_world
    out = [m @ v.co for v in me.vertices]; ev.to_mesh_clear(); return out

def clear_pose():
    for pb in arm.pose.bones:
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.location = (0, 0, 0); pb.scale = (1, 1, 1)
    bpy.context.view_layer.update()

clear_pose()
rest = deformed()
rlen = [(rest[a] - rest[b]).length for a, b in edges]

# 몸 부위 분류 (레스트 좌표: Y 앞뒤(코=-Y), Z 위아래)
def region(p):
    x, y, z = p
    if y < -0.30 and z > 0.38: return "머리"
    if -0.32 < y < -0.15 and 0.12 < z < 0.40: return "목/어깨/겨드랑이"
    if z > 0.40 and -0.25 < y < 0.35: return "등(척추 위)"
    if y > 0.35: return "꼬리/엉덩이"
    if z < 0.15: return "다리/발"
    return "몸통 측면"

def dom(i):
    gw = sorted(((e.weight, groups.get(e.group, "?")) for e in mesh.data.vertices[i].groups), reverse=True)
    return gw[0][1] if gw else "(none)"

TESTS = ["Bip001 Neck", "Bip001 Head", "Bip001 Spine2", "Bip001 Spine1", "Bip001 Spine",
         "Bip001 L Clavicle", "Bip001 R Clavicle", "Bip001 L UpperArm", "Bip001 R UpperArm",
         "Bip001 L Thigh", "Bip001 R Thigh", "Tail_01"]

print("단일 본 %.0f도 회전 시 엣지 늘어남 (애니메이션 미사용)" % math.degrees(ANGLE))
print("%-22s %8s %8s   %s" % ("bone", "max", ">3x", "스파이크 위치"))
worst_overall = []
for bone in TESTS:
    if bone not in arm.pose.bones: continue
    best = [1.0] * len(edges)
    for axis in ('X', 'Y', 'Z'):
        for sign in (1, -1):
            clear_pose()
            q = arm.pose.bones[bone].rotation_quaternion
            import mathutils
            arm.pose.bones[bone].rotation_quaternion = mathutils.Quaternion(
                {'X': (1, 0, 0), 'Y': (0, 1, 0), 'Z': (0, 0, 1)}[axis], ANGLE * sign)
            bpy.context.view_layer.update()
            cur = deformed()
            for k, (a, b) in enumerate(edges):
                if rlen[k] < 1e-7: continue
                s = (cur[a] - cur[b]).length / rlen[k]
                if s > best[k]: best[k] = s
    mx = max(best)
    over3 = sum(1 for x in best if x > 3.0)
    idx = sorted(range(len(edges)), key=lambda k: -best[k])[:40]
    reg = Counter(region(rest[edges[k][0]]) for k in idx)
    top = ", ".join("%s:%d" % (r, c) for r, c in reg.most_common(3))
    print("%-22s %8.1fx %8d   %s" % (bone, mx, over3, top))
    if mx > 3.0:
        worst_overall.append((mx, bone, idx[:6]))

print("\n가장 심한 본의 문제 엣지 상세:")
worst_overall.sort(reverse=True)
for mx, bone, idx in worst_overall[:3]:
    print("  [%s] max %.1fx" % (bone, mx))
    for k in idx:
        a, b = edges[k]
        print("     v%-5d-v%-5d  rest=%s  region=%-18s bones: %s / %s"
              % (a, b, [round(x, 3) for x in rest[a]], region(rest[a]), dom(a), dom(b)))
