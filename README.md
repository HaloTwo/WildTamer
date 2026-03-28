# Wild Tamer Inspired Prototype (Unity 2D)

111퍼센트의 **Wild Tamer**를 참고하여 제작한  
군집 기반 테이밍 RPG **프로토타입 프로젝트**입니다.

플레이어는 야생 몬스터를 처치하여 일정 확률로 **테이밍**할 수 있으며  
동료들은 플레이어를 중심으로 **군집 형태로 이동하고 자동 전투**에 참여합니다.

---

## 📌 Project Info
- 개발 인원 : 1인  
- 개발 기간 : 2026.03.05 ~ 2026.03.08 (4일)   
- 개발 환경 : C#, Unity 2D, GitHub  

---

## 🎥 Gameplay Video
[![Watch the Gameplay](https://img.youtube.com/vi/mLd9Y7k4bvM/0.jpg)](https://youtube.com/shorts/mLd9Y7k4bvM)

---

## 핵심 구현 요소

- 플레이어를 중심으로 이동하는 **Squad 기반 동료 시스템**
- 야생 몬스터와 부대 간 **자동 전투 시스템**
- 몬스터 처치 후 **확률 기반 테이밍 시스템**
- 상태 기반 **Monster AI (Idle / Chase / Attack)**
- `Physics2D.OverlapCircle` 기반 **주변 적 탐색**
- **Object Pool 시스템**을 통한 유닛 관리
- **Minimap + Fog of War** 탐험 시스템
- JSON 기반 **데이터 관리 구조**

---

## 🔧 Core Systems (Code Reference)

- Squad Controller  
→ [`PlayerSquadController.cs`](https://github.com/HaloTwo/WildTamer/blob/main/Assets/3.Script/Unit/Player/PlayerSquadController.cs)

- Ally AI  
→ [`AllyBrain.cs`](https://github.com/HaloTwo/WildTamer/blob/main/Assets/3.Script/Unit/AllyBrain.cs)

- Monster AI  
→ [`EnemyBrain.cs`](https://github.com/HaloTwo/WildTamer/blob/main/Assets/3.Script/Unit/EnemyBrain.cs)

- Object Pool System  
→ [`ObjectPool.cs`](https://github.com/HaloTwo/WildTamer/blob/main/Assets/3.Script/Manager/ObjectPool.cs)

- Minimap Fog System  
→ [`MiniMapFog_WorldAccum.cs`](https://github.com/HaloTwo/WildTamer/blob/main/Assets/3.Script/MiniMapFog_WorldAccum.cs)

---



## 📎 Notes

본 프로젝트는 **Wild Tamer 스타일의 코어 시스템을 학습하기 위한 프로토타입**입니다.  
