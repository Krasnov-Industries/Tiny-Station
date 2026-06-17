entity-condition-guidebook-total-damage =
    { $max ->
        [2147483648] суммарный урон не менее {NATURALFIXED($min, 2)}
        *[other] { $min ->
                    [0] суммарный урон не более {NATURALFIXED($max, 2)}
                    *[other] суммарный урон от {NATURALFIXED($min, 2)} до {NATURALFIXED($max, 2)}
                 }
    }

entity-condition-guidebook-type-damage =
    { $max ->
        [2147483648] урон типа {$type} не менее {NATURALFIXED($min, 2)}
        *[other] { $min ->
                    [0] урон типа {$type} не более {NATURALFIXED($max, 2)}
                    *[other] урон типа {$type} от {NATURALFIXED($min, 2)} до {NATURALFIXED($max, 2)}
                 }
    }

entity-condition-guidebook-group-damage =
    { $max ->
        [2147483648] урон группы {$type} не менее {NATURALFIXED($min, 2)}.
        *[other] { $min ->
                    [0] урон группы {$type} не более {NATURALFIXED($max, 2)}.
                    *[other] урон группы {$type} от {NATURALFIXED($min, 2)} до {NATURALFIXED($max, 2)}
                 }
    }

entity-condition-guidebook-total-hunger =
    { $max ->
        [2147483648] у цели суммарный голод не менее {NATURALFIXED($min, 2)}
        *[other] { $min ->
                    [0] у цели суммарный голод не более {NATURALFIXED($max, 2)}
                    *[other] у цели суммарный голод от {NATURALFIXED($min, 2)} до {NATURALFIXED($max, 2)}
                 }
    }

entity-condition-guidebook-reagent-threshold =
    { $max ->
        [2147483648] имеется не менее {NATURALFIXED($min, 2)}ед. {$reagent}
        *[other] { $min ->
                    [0] имеется не более {NATURALFIXED($max, 2)}ед. {$reagent}
                    *[other] имеется от {NATURALFIXED($min, 2)}ед. до {NATURALFIXED($max, 2)}ед. {$reagent}
                 }
    }

entity-condition-guidebook-mob-state-condition =
    существо в состоянии { $state }

entity-condition-guidebook-job-condition =
    должность цели — { $job }

entity-condition-guidebook-solution-temperature =
    температура раствора { $max ->
            [2147483648] не менее {NATURALFIXED($min, 2)}K
            *[other] { $min ->
                        [0] не более {NATURALFIXED($max, 2)}K
                        *[other] от {NATURALFIXED($min, 2)}K до {NATURALFIXED($max, 2)}K
                     }
    }

entity-condition-guidebook-body-temperature =
    температура тела { $max ->
            [2147483648] не менее {NATURALFIXED($min, 2)}K
            *[other] { $min ->
                        [0] не более {NATURALFIXED($max, 2)}K
                        *[other] от {NATURALFIXED($min, 2)}K до {NATURALFIXED($max, 2)}K
                     }
    }

entity-condition-guidebook-organ-type =
    метаболизирующий орган { $shouldhave ->
                                [true] является
                                *[false] не является
                           } органом «{$name}»

entity-condition-guidebook-has-tag =
    у цели { $invert ->
                 [true] нет тега
                 *[false] есть тег
                } {$tag}

entity-condition-guidebook-this-reagent = этот реагент

entity-condition-guidebook-breathing =
    метаболизатор { $isBreathing ->
                [true] дышит нормально
                *[false] задыхается
               }

entity-condition-guidebook-internals =
    метаболизатор { $usingInternals ->
                [true] использует внутренние баллоны
                *[false] дышит атмосферным воздухом
               }
