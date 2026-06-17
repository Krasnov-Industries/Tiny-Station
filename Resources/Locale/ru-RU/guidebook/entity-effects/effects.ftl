entity-effect-guidebook-spawn-entity =
    { $chance ->
        [1] Creates
        *[other] create
    } { $amount ->
        [1] {INDEFINITE($entname)}
        *[other] {$amount} {MAKEPLURAL($entname)}
    }

entity-effect-guidebook-destroy =
    { $chance ->
        [1] Destroys
        *[other] destroy
    } the object

entity-effect-guidebook-break =
    { $chance ->
        [1] Breaks
        *[other] break
    } the object

entity-effect-guidebook-explosion =
    { $chance ->
        [1] Causes
        *[other] cause
    } an explosion

entity-effect-guidebook-emp =
    { $chance ->
        [1] Вызывает
        *[other] вызвать
    } электромагнитный импульс

entity-effect-guidebook-flash =
    { $chance ->
        [1] Causes
        *[other] cause
    } a blinding flash

entity-effect-guidebook-foam-area =
    { $chance ->
        [1] Создаёт
        *[other] создать
    } большое количество пены

entity-effect-guidebook-smoke-area =
    { $chance ->
        [1] Создаёт
        *[other] создать
    } большое количество дыма

entity-effect-guidebook-satiate-thirst =
    { $chance ->
        [1] Утоляет
        *[other] утолить
    } { $relative ->
        [1] жажду со средней скоростью
        *[other] жажду со скоростью {NATURALFIXED($relative, 3)}x от средней
    }

entity-effect-guidebook-satiate-hunger =
    { $chance ->
        [1] Утоляет
        *[other] утолить
    } { $relative ->
        [1] голод со средней скоростью
        *[other] голод со скоростью {NATURALFIXED($relative, 3)}x от средней
    }

entity-effect-guidebook-health-change =
    { $chance ->
        [1] { $healsordeals ->
                [heals] Heals
                [deals] Deals
                *[both] Modifies health by
             }
        *[other] { $healsordeals ->
                    [heals] heal
                    [deals] deal
                    *[both] modify health by
                 }
    } { $changes }

entity-effect-guidebook-even-health-change =
    { $chance ->
        [1] { $healsordeals ->
            [heals] Evenly heals
            [deals] Evenly deals
            *[both] Evenly modifies health by
        }
        *[other] { $healsordeals ->
            [heals] evenly heal
            [deals] evenly deal
            *[both] evenly modify health by
        }
    } { $changes }

entity-effect-guidebook-status-effect-old =
    { $type ->
        [update]{ $chance ->
                    [1] Вызывает
                     *[other] вызвать
                 } {LOC($key)} минимум на {NATURALFIXED($time, 3)} сек. без накопления
        [add]   { $chance ->
                    [1] Вызывает
                    *[other] вызвать
                } {LOC($key)} минимум на {NATURALFIXED($time, 3)} сек. с накоплением
        [set]  { $chance ->
                    [1] Вызывает
                    *[other] вызвать
                } {LOC($key)} на {NATURALFIXED($time, 3)} сек. без накопления
        *[remove]{ $chance ->
                    [1] Снимает
                    *[other] снять
                } {NATURALFIXED($time, 3)} сек. эффекта {LOC($key)}
    }

entity-effect-guidebook-status-effect =
    { $type ->
        [update]{ $chance ->
                    [1] Вызывает
                    *[other] вызвать
                 } {LOC($key)} минимум на {NATURALFIXED($time, 3)} сек. без накопления
        [add]   { $chance ->
                    [1] Вызывает
                    *[other] вызвать
                } {LOC($key)} минимум на {NATURALFIXED($time, 3)} сек. с накоплением
        [set]  { $chance ->
                    [1] Вызывает
                    *[other] вызвать
                } {LOC($key)} минимум на {NATURALFIXED($time, 3)} сек. без накопления
        *[remove]{ $chance ->
                    [1] Снимает
                    *[other] снять
                } {NATURALFIXED($time, 3)} сек. эффекта {LOC($key)}
    } { $delay ->
        [0] немедленно
        *[other] с задержкой {NATURALFIXED($delay, 3)} сек.
    }

entity-effect-guidebook-status-effect-indef =
    { $type ->
        [update]{ $chance ->
                    [1] Вызывает
                    *[other] вызвать
                 } постоянный {LOC($key)}
        [add]   { $chance ->
                    [1] Вызывает
                    *[other] вызвать
                } постоянный {LOC($key)}
        [set]  { $chance ->
                    [1] Вызывает
                    *[other] вызвать
                } постоянный {LOC($key)}
        *[remove]{ $chance ->
                    [1] Снимает
                    *[other] снять
                } {LOC($key)}
    } { $delay ->
        [0] немедленно
        *[other] с задержкой {NATURALFIXED($delay, 3)} сек.
    }

entity-effect-guidebook-knockdown =
    { $type ->
        [update]{ $chance ->
                    [1] Вызывает
                    *[other] вызвать
                    } {LOC($key)} минимум на {NATURALFIXED($time, 3)} сек. без накопления
        [add]   { $chance ->
                    [1] Вызывает
                    *[other] вызвать
                } сбивание с ног минимум на {NATURALFIXED($time, 3)} сек. с накоплением
        *[set]  { $chance ->
                    [1] Вызывает
                    *[other] вызвать
                } сбивание с ног минимум на {NATURALFIXED($time, 3)} сек. без накопления
        [remove]{ $chance ->
                    [1] Снимает
                    *[other] снять
                } {NATURALFIXED($time, 3)} сек. сбивания с ног
    }

entity-effect-guidebook-set-solution-temperature-effect =
    { $chance ->
        [1] Устанавливает
        *[other] установить
    } температуру раствора ровно на {NATURALFIXED($temperature, 2)}K

entity-effect-guidebook-adjust-solution-temperature-effect =
    { $chance ->
        [1] { $deltasign ->
                [1] Добавляет
                *[-1] Убавляет
            }
        *[other]
            { $deltasign ->
                [1] добавить
                *[-1] убавить
            }
    } тепло раствора, пока он не достигнет { $deltasign ->
                [1] не более {NATURALFIXED($maxtemp, 2)}K
                *[-1] не менее {NATURALFIXED($mintemp, 2)}K
            }

entity-effect-guidebook-adjust-reagent-reagent =
    { $chance ->
        [1] { $deltasign ->
                [1] Добавляет
                *[-1] Удаляет
            }
        *[other]
            { $deltasign ->
                [1] добавить
                *[-1] удалить
            }
    } {NATURALFIXED($amount, 2)}ед. {$reagent} { $deltasign ->
        [1] в
        *[-1] из
    } раствор(а)

entity-effect-guidebook-adjust-reagent-group =
    { $chance ->
        [1] { $deltasign ->
                [1] Добавляет
                *[-1] Удаляет
            }
        *[other]
            { $deltasign ->
                [1] добавить
                *[-1] удалить
            }
    } {NATURALFIXED($amount, 2)}ед. реагентов группы {$group} { $deltasign ->
            [1] в
            *[-1] из
        } раствор(а)

entity-effect-guidebook-adjust-temperature =
    { $chance ->
        [1] { $deltasign ->
                [1] Добавляет
                *[-1] Убавляет
            }
        *[other]
            { $deltasign ->
                [1] добавить
                *[-1] убавить
            }
    } {POWERJOULES($amount)} тепла { $deltasign ->
            [1] в
            *[-1] из
        } тело, в котором находится

entity-effect-guidebook-chem-cause-disease =
    { $chance ->
        [1] Causes
        *[other] cause
    } the disease { $disease }

entity-effect-guidebook-chem-cause-random-disease =
    { $chance ->
        [1] Causes
        *[other] cause
    } the diseases { $diseases }

entity-effect-guidebook-jittering =
    { $chance ->
        [1] Causes
        *[other] cause
    } jittering

entity-effect-guidebook-clean-bloodstream =
    { $chance ->
        [1] Очищает
        *[other] очистить
    } кровь от других химикатов

entity-effect-guidebook-cure-disease =
    { $chance ->
        [1] Cures
        *[other] cure
    } diseases

entity-effect-guidebook-eye-damage =
    { $chance ->
        [1] { $deltasign ->
                [1] Deals
                *[-1] Heals
            }
        *[other]
            { $deltasign ->
                [1] deal
                *[-1] heal
            }
    } eye damage

entity-effect-guidebook-vomit =
    { $chance ->
        [1] Causes
        *[other] cause
    } vomiting

entity-effect-guidebook-create-gas =
    { $chance ->
        [1] Creates
        *[other] create
    } { $moles } { $moles ->
        [1] mole
        *[other] moles
    } of { $gas }

entity-effect-guidebook-drunk =
    { $chance ->
        [1] Causes
        *[other] cause
    } drunkness

entity-effect-guidebook-electrocute =
    { $chance ->
        [1] { $stuns ->
            [true] Бьёт током
            *[false] Шокирует
            }
        *[other] { $stuns ->
            [true] ударить током
            *[false] шокировать
            }
    } метаболизатора на {NATURALFIXED($time, 3)} сек.

entity-effect-guidebook-emote =
    { $chance ->
        [1] Заставит
        *[other] заставить
    } метаболизатора [bold][color=white]{$emote}[/color][/bold]

entity-effect-guidebook-extinguish-reaction =
    { $chance ->
        [1] Extinguishes
        *[other] extinguish
    } fire

entity-effect-guidebook-flammable-reaction =
    { $chance ->
        [1] Increases
        *[other] increase
    } flammability

entity-effect-guidebook-ignite =
    { $chance ->
        [1] Ignites
        *[other] ignite
    } the metabolizer

entity-effect-guidebook-make-sentient =
    { $chance ->
        [1] Делает
        *[other] сделать
    } метаболизатора разумным

entity-effect-guidebook-make-polymorph =
    { $chance ->
        [1] Превращает
        *[other] превратить
    } метаболизатора в { $entityname }

entity-effect-guidebook-modify-bleed-amount =
    { $chance ->
        [1] { $deltasign ->
                [1] Induces
                *[-1] Reduces
            }
        *[other] { $deltasign ->
                    [1] induce
                    *[-1] reduce
                 }
    } bleeding

entity-effect-guidebook-modify-blood-level =
    { $chance ->
        [1] { $deltasign ->
                [1] Increases
                *[-1] Decreases
            }
        *[other] { $deltasign ->
                    [1] increases
                    *[-1] decreases
                 }
    } blood level

entity-effect-guidebook-paralyze =
    { $chance ->
        [1] Парализует
        *[other] парализовать
    } метаболизатора минимум на {NATURALFIXED($time, 3)} сек.

entity-effect-guidebook-movespeed-modifier =
    { $chance ->
        [1] Изменяет
        *[other] изменить
    } скорость передвижения в {NATURALFIXED($sprintspeed, 3)}x минимум на {NATURALFIXED($time, 3)} сек.

entity-effect-guidebook-reset-narcolepsy =
    { $chance ->
        [1] Temporarily staves
        *[other] temporarily stave
    } off narcolepsy

entity-effect-guidebook-wash-cream-pie-reaction =
    { $chance ->
        [1] Смывает
        *[other] смыть
    } крем с лица

entity-effect-guidebook-cure-zombie-infection =
    { $chance ->
        [1] Лечит
        *[other] вылечить
    } текущую зомби-инфекцию

entity-effect-guidebook-cause-zombie-infection =
    { $chance ->
        [1] Заражает
        *[other] заразить
    } существо зомби-инфекцией

entity-effect-guidebook-innoculate-zombie-infection =
    { $chance ->
        [1] Лечит
        *[other] вылечить
    } текущую зомби-инфекцию и даёт иммунитет к будущим заражениям

entity-effect-guidebook-reduce-rotting =
    { $chance ->
        [1] Regenerates
        *[other] regenerate
    } {NATURALFIXED($time, 3)} {MANY("second", $time)} of rotting

entity-effect-guidebook-area-reaction =
    { $chance ->
        [1] Вызывает
        *[other] вызвать
    } реакцию дыма или пены на {NATURALFIXED($duration, 3)} сек.

entity-effect-guidebook-add-to-solution-reaction =
    { $chance ->
        [1] Добавляет
        *[other] добавить
    } {$reagent} во внутренний контейнер с раствором

entity-effect-guidebook-artifact-unlock =
    { $chance ->
        [1] Помогает
        *[other] помочь
        } разблокировать инопланетный артефакт.

entity-effect-guidebook-artifact-durability-restore =
    Восстанавливает {$restored} прочности в активных узлах инопланетного артефакта.

entity-effect-guidebook-plant-attribute =
    { $chance ->
        [1] Adjusts
        *[other] adjust
    } {$attribute} by {$positive ->
    [true] [color=red]{$amount}[/color]
    *[false] [color=green]{$amount}[/color]
    }

entity-effect-guidebook-plant-cryoxadone =
    { $chance ->
        [1] Омолаживает
        *[other] омолодить
    } растение в зависимости от его возраста и времени роста

entity-effect-guidebook-plant-phalanximine =
    { $chance ->
        [1] Восстанавливает
        *[other] восстановить
    } жизнеспособность растения, утратившего её из-за мутации

entity-effect-guidebook-plant-diethylamine =
    { $chance ->
        [1] Увеличивает
        *[other] увеличить
    } срок жизни и/или базовое здоровье растения с вероятностью 10% для каждого

entity-effect-guidebook-plant-robust-harvest =
    { $chance ->
        [1] Увеличивает
        *[other] увеличить
    } потенцию растения на {$increase}, вплоть до максимума {$limit}. Когда потенция достигает {$seedlesstreshold}, растение теряет семена. Попытка поднять потенцию выше {$limit} может с вероятностью 10% снизить урожайность

entity-effect-guidebook-plant-seeds-add =
    { $chance ->
        [1] Восстанавливает
        *[other] восстановить
    } семена растения

entity-effect-guidebook-plant-seeds-remove =
    { $chance ->
        [1] Удаляет
        *[other] удалить
    } семена растения

-create-3rd-person =
    { $chance ->
        [1] Создаёт
        *[other] создают
    }

entity-effect-guidebook-plant-mutate-chemicals =
    { $chance ->
        [1] Мутирует
        *[other] мутировать
    } растение, чтобы оно производило {$name}

entity-effect-guidebook-plant-remove-kudzu =
    { $chance ->
        [1] Удаляет
        *[other] удалить
    } разрастание кудзу с растения

