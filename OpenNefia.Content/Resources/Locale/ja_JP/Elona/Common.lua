Elona.Common = {
    Quotes = function(str)
        return ("「%s」"):format(str)
    end,
    ItIsImpossible = "それは無理だ。",
    NothingHappens = "何もおきない… ",
    SomethingIsPut = "何かが足元に転がってきた。",
    TooExhausted = function(entity)
        entity = entity or _.player()
        return ("%s疲労し過ぎて失敗した！"):format(_.sore_wa(entity))
    end,
    PutInBackpack = function(item)
        return ("%sをバックパックに入れた。"):format(_.name(item))
    end,
    CannotDoInGlobal = "その行為は、ワールドマップにいる間はできない。",
    DoesNotWorkHere = "この場所では効果がない。",
    NameWithDirectArticle = function(entity)
        return _.name(entity, true)
    end,
    QualifiedName = function(basename, itemTypeName)
        return basename
    end,
}
