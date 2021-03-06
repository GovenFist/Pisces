	CHARACTER*1 MRK(8)
	CHARACTER*2 IY,PC(1),PCD(50),PCE(50),PND(8),TT
	CHARACTER*3 P1(50),P2(50),MON(12)
	CHARACTER*4 CBTT(50),CBTK(50),CBB(50),CBT(50),IYT
	CHARACTER*10 S,ADD
	CHARACTER*12 MARK
	CHARACTER*20 FT(50),TIT(50)
	CHARACTER*27 AP
C
	DIMENSION FN(50),FH(50),IFN(50),IFH(50),IFL(50),AVE(50),AV(12)
	DIMENSION MN(12),DATA(12),FL(50),FP(50),IAVE(50),IPN(50)
	DIMENSION IPH(50),IPL(50)
C
C
	INCLUDE 'DRA2:[MPOLL]OPEN2.DAT/LIST'
	INCLUDE 'SYS$LIBRARY:FORIOSDEF/LIST'
C
C
	DATA MON/'OCT','NOV','DEC','JAN','FEB','MAR','APR','MAY',
	1'JUN','JUL','AUG','SEP'/
	DATA MN/1,2,3,4,5,6,7,8,9,10,11,12/
	DATA PC/'15'/
	DATA PND/'60','61','62','63','64','65','66','67'/
	DATA MRK/'T','T','T','T','T','T','T','T'/
	AP='ABCDEFGHIJKLMNOPQRSTUVWXYZ '
	MARK='            '
C
C
1010	FORMAT(' ERROR IN READ UNIT=1, IOS=',I2,/,A12,6F11.2/12X,F11.2)
1008	FORMAT(' ATTEMPT TO ACCESS NON-EXISTENT RECORD IOS=',I2,1X,A)
1011	FORMAT(' ERROR ON REWRITE IOS=',I2,A12,6F11.2/12X,6F11.2)
1013	FORMAT(' ERROR ON OPEN UNIT=1, IOS=',I2)
1012	FORMAT(' ERROR ON WRITE TO INDEX FILE IOS=',I2,A12,6F11.2/12X,
	16F11.2)
1014	FORMAT(' ERROR ON CLOSE, IOS=',I2,',UNIT=1')
C
C
	I=1
101	READ(2,1000,END=100)TIT(I),CBT(I),PCD(I),CBB(I)
1000	FORMAT(A,1X,A,8X,A,1X,A)
	I=I+1
	GO TO 101
100	K=1
103	READ(3,1001,END=102)P1(K),P2(K),FT(K),FN(K),FH(K),FL(K),FP(K)
1001	FORMAT(1X,2A,2X,A,10X,4F9.1)
	K=K+1
	GO TO 103
102	NF=K-1
C
C
	DO 304 J=1,12
	IF(P1(1).EQ.MON(J))THEN
		MNT=MN(J)
		END IF
304	CONTINUE
	NT=I-1
C	IY='85'
	READ(5,1019)IY
1019	FORMAT(A)
	WRITE(6,1020)IY
1020	FORMAT(1X,A)
	IYT='19'//IY
	DO 300 K=1,NF
	DO 301 I=1,NT
	IF(FT(K).EQ.TIT(I))THEN
		CBTT(K)=CBT(I)
		PCE(K)=PCD(I)
		END IF
301	CONTINUE
300	CONTINUE
C
C
C	WRITE FORECASTS TO INDEX FILE
C**********************************************************************
	DO 307 K=1,NF
	DO 308 JN=1,1
	IF(JN.EQ.5)PC(JN)=PCE(K)
	ADD=CBTT(K)//PC(JN)//IYT
	DO 309 J=1,12
	DATA(J)=998877.
	MARK(J:J)=' '
309	CONTINUE
3010	READ(UNIT=1,KEY=ADD,KEYID=0,IOSTAT=IOS,ERR=8000)ADD,DATA,MARK
	IF(IOS.EQ.0)THEN
		DO 317 KI=1,12
		TT='NO'
		DO 316 JI=1,27
		IF(MARK(KI:KI).EQ.AP(JI:JI))TT='OK'
316		CONTINUE
		IF(TT.EQ.'NO')MARK(KI:KI)=' '
317		CONTINUE
		MARK(MNT:MNT)='T'
		DATA(MNT)=FP(K)
		REWRITE(UNIT=1,IOSTAT=IOS,ERR=8001)ADD,DATA,MARK
		UNLOCK 1
		GO TO 308
		END IF
8000	IF(IOS.EQ.52)CALL RECLOCK(*3010,*5000)
	IF(IOS.EQ.36)THEN	
		MARK(MNT:MNT)='T'
		DATA(MNT)=FP(K)
		DO 314 J=1,7
		IF(PCE(K).EQ.PND(J))MARK(MNT:MNT)=MRK(J)
314		CONTINUE
		WRITE(UNIT=1,IOSTAT=IOS,ERR=5001)ADD,DATA,MARK
		UNLOCK 1
		GO TO 308
		END IF
	WRITE(6,1010)IOS,ADD,DATA
	GO TO 5000
308	CONTINUE
307	CONTINUE
C
C
C	END OF WRITE TO INDEX FILE
C***********************************************************************
C
	WRITE(6,1018)
1018	FORMAT(' MPOLL UPDATED!!!!!!')
	GO TO 5000
8001	WRITE(6,1012)IOS,ADD,DATA
	GO TO 5000
5001	WRITE(6,1012)IOS,ADD,DATA
	GO TO 5000
9999	WRITE(6,1013)IOS
	GO TO 5003
5000	CLOSE(UNIT=1,ERR=5002,IOSTAT=IOS)
	GO TO 5003
5002	WRITE(6,1014)IOS
	GO TO 5003
5003	STOP
	END

