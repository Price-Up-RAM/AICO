'''Walk 클립에서 이어붙여도 안 튀는 최적 루프 구간 [a,b) 를 찾는다. 다리/꼬리 신호를 분리해서 본다.'''
import bpy, sys, math
argv = sys.argv[sys.argv.index("--")+1:]
fbx, want = argv[0], argv[1]

LEGS = ["Bip001 L Thigh","Bip001 R Thigh","Bip001 L UpperArm","Bip001 R UpperArm",
        "Bip001 L Calf","Bip001 R Calf","Bip001 L Forearm","Bip001 R Forearm",
        "Bip001 L Foot","Bip001 R Foot","Bip001 L Hand","Bip001 R Hand"]
TAIL = ["Tail_01","Tail_02","Tail_03"]
ALL  = None

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.wm.fbx_import(filepath=fbx)
arm=next(o for o in bpy.data.objects if o.type=='ARMATURE')
scn=bpy.context.scene
act=next(a for a in bpy.data.actions if want.lower() in a.name.lower())
if arm.animation_data is None: arm.animation_data_create()
arm.animation_data.action=act
sl=list(getattr(act,"slots",[]))
if sl: arm.animation_data.action_slot=sl[0]
f0,f1=[int(round(v)) for v in act.frame_range]; n=f1-f0+1
ALL=[b.name for b in arm.data.bones if not any(k in b.name.lower() for k in ("eye","nose","mouth","tongue"))]

snap=[]
for i in range(n):
    scn.frame_set(f0+i)
    snap.append({b: arm.pose.bones[b].rotation_quaternion.copy() for b in ALL})

def d(i,j,bones):
    s=0.0
    for b in bones:
        q=snap[i][b].rotation_difference(snap[j][b]).angle
        if q>math.pi: q=2*math.pi-q
        s+=q*q
    return math.degrees(math.sqrt(s/len(bones)))

leg_vel=[0.0]+[d(i-1,i,LEGS) for i in range(1,n)]
tail_vel=[0.0]+[d(i-1,i,TAIL) for i in range(1,n)]
thr=max(leg_vel)*0.12
act_idx=[i for i,v in enumerate(leg_vel) if v>thr]
lo,hi=(min(act_idx),max(act_idx)) if act_idx else (0,n-1)
print(f"clip={act.name} n={n} frames {f0}..{f1}")
print(f"다리 활성 구간: f{f0+lo} .. f{f0+hi}  ({hi-lo+1} frames)")
print(f"다리 총 이동량 {sum(leg_vel):7.1f}deg | 꼬리 총 이동량 {sum(tail_vel):7.1f}deg")
print(f"  정지구간(f{f0}..f{f0+lo-1}) 꼬리 이동량 {sum(tail_vel[:lo]):6.1f}deg  다리 {sum(leg_vel[:lo]):6.1f}deg  <- '꼬리만 흔드는' 구간 확인")
print(f"  후미(f{f0+hi+1}..f{f1}) 꼬리 {sum(tail_vel[hi+1:]):6.1f}deg 다리 {sum(leg_vel[hi+1:]):6.1f}deg")

print("\n최적 루프 구간 후보 (다리 기준 seam, 길이 12프레임 이상):")
res=[]
for a in range(max(0,lo-4), hi):
    for b in range(a+12, min(n, hi+5)):
        res.append((d(a,b,LEGS), d(a,b,ALL), a, b))
res.sort()
for seamL, seamA, a, b in res[:10]:
    print(f"  [f{f0+a:3d} .. f{f0+b:3d}]  len={b-a:3d}f ({(b-a)/30.0:.2f}s)  seam(legs)={seamL:5.2f}deg  seam(all)={seamA:5.2f}deg")
