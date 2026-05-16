# Space Whale — Legacy / откатано в "топорной" версии

Этот файл — память о том, что у кита было раньше, и что можно вернуть когда понадобится.
Бэкап рабочего кода: `~/.claude-work/artifacts/whale_simplify_<timestamp>/`.

## Текущая модель (минимум)

- **HP**: 4000
- **Поведение**: подлетает к станции, кружит в 10 тайлах от борта.
  Видит цели через LOS в радиусе 30, "чует" сквозь стены в радиусе 15.
  Атакует ближайшую цель.
- **Атаки**:
  - Bite (через MeleeWeapon, 1 раз/сек)
  - Roar (AoE Stun 2с + Slow 5с + Jitter 3с + camera shake, cd 10 сек)
- **Ломает постройки**: `DamageOnCollideComponent` — Structural 5000 + Blunt 10
  при любой коллизии. Без cooldown, без проверок, тупо при контакте.
- **Аура**: гасит лампы в радиусе 6 (восстанавливаются через 60 сек),
  спавнит ложные radar-blip'ы.
- **Поедание трупов**: +500 HP за труп, желудок до 5 минут.
- **Хвост-змейка**: 65 сегментов с волновой анимацией.
- **TopAggressor**: запоминает того, кто за последние 30 сек больше всех урона нанёс — идёт за ним приоритетно.
- **Spawn**: 4 триггера Awakening (ядерка, C4 в космосе, корабельные орудия, далёкий игрок 10 мин), затем Threat ≥ 40 → spawn.

## Что было снято (можно вернуть)

### Фазы по HP — Hunt / Frenzy / Rampage
- Hunt 66–100%, Frenzy 33–66%, Rampage <33%.
- В каждой фазе свои cooldown'ы Roar/Ram, скорость, поиск.
- Rampage отключал LOS, был необратим (`RampageLocked`).
- Реализация: `WhalePhaseSystem` + `WhalePhaseComponent`, `WhaleInPhasePrecondition`.

### Tail Swipe
- Telegraph 1.5 сек (звук `whale-tail-warn.ogg` + красная подсветка сегментов).
- Удар по гуманоидам вокруг каждого сегмента хвоста в радиусе 1.5.
- Blunt 60 + throw 8 тайлов + Paralyze 2с.
- Cd 25 сек.
- Реализация: `HTNTailSwipeOperator` + `WhaleAbilitySystem.TryTailSwipe` + `ApplyTailSwipe` +
  `WhaleAbilityComponent.NextTailSwipe/TailSwipeImpactAt/TailSwipePending`.

### Ram (рывок)
- Импульс mass × 30 в сторону цели + Blunt 100 + knockback.
- Cd 6–12 (по фазе).
- Реализация: `HTNRamOperator` + `WhaleAbilitySystem.TryRam` + `MarkRamIntent` +
  `WhaleAbilityComponent.NextRam`. + `DamageOnCollideComponent.RamIntentUntil`.

### R5 — Отступление от группы (Retreat)
- 3+ вооружённых в радиусе 10 + урон > N за 3 сек → отходит на 15 тайлов,
  регенерация 10 HP/сек × 8 сек = +80 HP.
- Cd 40 сек, пороги: Hunt 300 урона/3с, Frenzy 800/3с, Rampage — отключено.
- Реализация: `HTNRetreatOperator` + `WhaleAbilitySystem.TryRetreat` +
  `GroupOfArmedNearbyPrecondition` + `RecentDamagePrecondition` + `CooldownPrecondition` +
  `WhaleCooldownComponent` + `WhaleDamageHistoryComponent`.

### R8 — Pain Learning (резисты по типу)
- За 400+ урона одного типа за 10 сек → +15% резист к этому типу на 20 сек.
- Max 2 типа одновременно.
- Реализация: `WhalePainLearningSystem` + `WhalePainLearningComponent`.

### Нюх — память на убийства (RecentKills)
- Список из 5 точек последних убийств с TTL 5 мин, агрегация близких в радиусе 10.
- Когда нет других целей — случайная точка из памяти как навигационная цель.
- Отключён в Rampage.
- Реализация: `HasFreshKillLocationPrecondition` + `HTNMoveToLastKilledOperator` +
  `WhaleMemoryComponent.RecentKills` + `WhaleKillRecord` +
  `WhaleMemorySystem.OnMobStateChanged/RegisterKill/PurgeOldKills`.

### RadarBlip — отображение кита на радаре
- На голове, всех 65 сегментах хвоста и spoof-эхо стоял `RadarBlip` с разной формой
  (hexagon/diamond/circle) и `visibleFromOtherGrids: true`.
- Идея: кит виден на радаре любой консоли станции как огромный гексагон, за ним
  тянется змейка ромбов хвоста, а вокруг — мерцающие круги-помехи от ауры.
- **Не работало**: в Tiny-Station апстриме нет системы, обрабатывающей `RadarBlipComponent`.
  Компонент был определён только в `Content.Shared/_Goobstation/SpaceWhale/SpaceWhaleVisualComponents.cs`,
  ни одно место в коде его не читало → на радаре кит был невидим.
- Что выпилено:
  - `RadarBlipComponent` в `SpaceWhaleVisualComponents.cs`
  - `SpaceWhaleRadarSpoofComponent` + `SpaceWhaleRadarSpoofSystem`
  - Прототип `SpaceWhaleRadarSpoof`
  - `WhaleAuraComponent.RadarSpoofRadius` + spawn spoof'ов в `WhaleAuraSystem`
  - `RadarBlip` секции в `whale.yml` у `SpaceLeviathanBase` и `SpaceWhaleSegment`
- Чтобы вернуть: либо порт клиентской системы рендера blip'ов из Goob-Station,
  либо привязка кита к фантомному гриду с `IFFComponent`, либо отдельный overlay
  в `Content.Client/Shuttles/UI/ShuttleNavControl.xaml.cs`.

### Радио-глушение через RadioJammerComponent
- Аура глушила рацию в радиусе 6.
- Сломалось из-за бага в апстримовом `SharedJammerSystem.OnGetVerb` (NRE при null Settings).
- Реализация: было в `WhaleAuraSystem.TickAura`, использовало штатный `RadioJammerComponent` +
  `ActiveRadioJammerComponent`. Можно вернуть только через свой кастомный jammer.

### Шум-карта как навигация (HasLoudNoise / MoveToNoiseSource)
- Кит сворачивал на громкий шум по фазе (Hunt 1 / Frenzy 15 / Rampage 50 intensity).
- Реализация хранится в `WhaleThreatComponent.RecentNoises` + `PickBestNoiseFor`.
- Сама шум-карта осталась — она нужна для эмбиента и Threat-пула.
- Поведенческой ветки "идти к шуму" больше нет.

### LOS-фильтрация целей (`WhaleTargeting`)
- При выборе цели проверялся `InRangeUnobstructed` со SightMask =
  Opaque | Impassable | InteractImpassable.
- Был параметр `obstructedRadius` — зона "чую без LOS" вокруг кита.
- Реализация: `WhaleTargeting.cs`.

### Орбита по фазам
- HTNMoveToStationOperator считал точку на круге `orbitDistance × advanceRadians`.
- Hunt: 35 тайлов, advance 0.7; Frenzy: 24, 1.0; Rampage: 12, 1.2 (у крупнейшей станции).
- Investigation point с standoff (HTNMoveToNoiseSourceOperator):
  если шум возле станции — кит вставал на орбиту, не лез в борт.
- Реализация: `TryGetStationOrbitPoint`, `TryGetNearestStationOrbitPoint`,
  `TryGetLargestStationOrbitPoint`, `TryGetInvestigationPoint` в `WhaleThreatSystem`.

### MovementIntent в DamageOnCollide
- `DamageOnCollideComponent.MovementIntentUntil/RamIntentUntil/MovementIntentGrace`.
- Стена ломалась только когда кит активно двигается или таранит (а не при пассивном касании).
- Решало проблему "кит висит у борта и зря долбит стенку".
- Реализация: подписка на ход + cooldown per target в `DamageOnCollideSystem`.

### HTN AI — 18 веток, 10 операторов, 11 preconditions
- Hunt: Retreat → AttackTopAggressor(LOS) → PickClosestMob(LOS) → InvestigateNoise → "scent" PickClosestMob(no LOS) → MoveToLastKilled → MoveToStation orbit.
- Frenzy и Rampage по своим веткам.
- Файлы:
  - `Resources/Prototypes/_Goobstation/NPCs/HTN/space_whale.yml`
  - `Content.Server/_Goobstation/SpaceWhale/AI/HTN/SpaceWhaleHtnOperators.cs`
  - `Content.Server/_Goobstation/SpaceWhale/AI/Preconditions/SpaceWhalePreconditions.cs`

### Админ-команды (удалены)
- `whalephase <hunt|frenzy|rampage>` — установка фазы.
- `whalerage` — мгновенный Rampage.
- `whaleram` — форс Ram.
- `whaletailswipe` — форс Tail Swipe.
- `whaleresistdump` — дамп R8 резистов.

## CVars, оставшиеся "на будущее" в `CCVars.SpaceWhale.cs`

Я их **не удалял** — пусть лежат, занимают мало места, можно вернуть фичи быстро:
- `whale.threat_decay`, `whale.threat_spawn_at` — используются.
- `whale.rampage_hp_percent`, `whale.frenzy_hp_percent` — фаз больше нет, можно убрать.
- `whale.noise_*` — частично используются (для AddNoise/PickBestNoiseFor — оставлены под Threat-пул).
- `whale.r8_*` — для pain learning, не используются.
- `whale.consume_heal`, `whale.stomach_cleanup_sec` — используются.
- `whale.last_killed_max_age` — для RecentKills, не используется.
- `whale.rampage_mob_search_radius`, `whale.retreat_hunt_damage`, `whale.retreat_frenzy_damage` — не используются.

## Куда смотреть, если хочется вернуть

Базовая идея вернуть удалённое:
1. Восстановить файлы из `~/.claude-work/artifacts/whale_simplify_<timestamp>/`.
2. Подключить компоненты обратно в `whale.yml`.
3. Подключить HTN или перенести логику в WhaleBrainSystem как отдельные ветви.
